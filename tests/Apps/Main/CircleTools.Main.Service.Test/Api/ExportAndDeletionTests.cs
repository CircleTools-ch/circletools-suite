using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Covers data export (FR-014) and account deletion (FR-015, ADR-0022).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class ExportAndDeletionTests(MailApiFactory factory)
{
    private const string Password = "correct-horse-battery";

    private static string UniqueEmail() => $"lifecycle-{Guid.CreateVersion7():N}@example.test";

    private async Task<int> DispatchOutboxAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxDispatcher>()
            .DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
    }

    private async Task<(string Email, HttpClient Client, SessionResponse Session)> SignedInAsync()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, Password, "en"));
        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var (userId, token) = LinkParser.Parse(body);

        await client.PostAsJsonAsync("/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return (email, client, session);
    }

    [Fact]
    public async Task Export_ContainsTheCallersOwnProfile()
    {
        var (email, client, _) = await SignedInAsync();

        var response = await client.PostAsync(new Uri("/api/v1/account/export", UriKind.Relative), content: null);

        response.EnsureSuccessStatusCode();
        var export = await response.Content.ReadFromJsonAsync<DataExport>();
        Assert.NotNull(export);
        Assert.Equal(email, export.Profile.Email);
        Assert.Equal("Anna", export.Profile.DisplayName);
        Assert.True(export.Profile.EmailVerified);
        Assert.Equal(1, export.Sessions);
    }

    [Fact]
    public async Task Export_ExplainsWhatIsDeliberatelyAbsent()
    {
        // An M1 archive is small because circles and synced groups do not exist yet. Saying so
        // stops a thin archive being read as data loss.
        var (_, client, _) = await SignedInAsync();

        var export = await (await client.PostAsync(new Uri("/api/v1/account/export", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<DataExport>();

        Assert.NotNull(export);
        Assert.NotEmpty(export.Notes);
    }

    [Fact]
    public async Task Export_NeverReturnsAnotherUsersData()
    {
        var (_, first, _) = await SignedInAsync();
        var (secondEmail, second, _) = await SignedInAsync();

        var firstExport = await (await first.PostAsync(new Uri("/api/v1/account/export", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<DataExport>();
        var secondExport = await (await second.PostAsync(new Uri("/api/v1/account/export", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<DataExport>();

        Assert.NotEqual(firstExport!.Profile.Id, secondExport!.Profile.Id);
        Assert.NotEqual(secondEmail, firstExport.Profile.Email);
    }

    [Fact]
    public async Task Export_RequiresAuthentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsync(new Uri("/api/v1/account/export", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheAccountAndEndsEverySession()
    {
        var (email, client, session) = await SignedInAsync();

        var deleted = await client.PostAsJsonAsync(
            "/api/v1/account/delete", new DeleteAccountRequest(Password));
        deleted.EnsureSuccessStatusCode();

        using var anonymous = factory.CreateClient();

        // The account is gone: the address no longer signs in.
        var login = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // And the session it held is dead.
        var refresh = await anonymous.PostAsJsonAsync(
            "/api/v1/account/refresh", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutTheCorrectPassword_IsRefused()
    {
        // Deletion is irreversible, so a session left open on a shared device must not be enough.
        var (email, client, _) = await SignedInAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/delete", new DeleteAccountRequest("not-the-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var anonymous = factory.CreateClient();
        var stillWorks = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, Password));
        stillWorks.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Delete_RequiresAuthentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/account/delete", new DeleteAccountRequest(Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByDefault_HandsOwnedSharedGroupsBackToSharedByLink()
    {
        // FR-043 / ADR-0058 default: the owner leaves, but the online group stays so everyone who
        // joined by link keeps access. It simply becomes owner-less again.
        var (_, client, _) = await SignedInAsync();
        var (accountId, group) = await OwnedSharedGroupFor(client);

        var deleted = await client.PostAsJsonAsync(
            "/api/v1/account/delete", new DeleteAccountRequest(Password, DeleteSharedGroups: false));
        deleted.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISharedGroupStore>();
        // The join link still resolves — participants are not stranded — but nobody owns it now.
        Assert.NotNull(await store.GetAsync(group.JoinToken, ParticipantProof.None));
        Assert.Empty(await store.ListOwnedAsync(accountId));
    }

    [Fact]
    public async Task Delete_WhenAsked_RemovesOwnedSharedGroupsToo()
    {
        // FR-043 / ADR-0058 alternative disposition: the owner chose to take the online copies with them.
        var (_, client, _) = await SignedInAsync();
        var (accountId, group) = await OwnedSharedGroupFor(client);

        var deleted = await client.PostAsJsonAsync(
            "/api/v1/account/delete", new DeleteAccountRequest(Password, DeleteSharedGroups: true));
        deleted.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISharedGroupStore>();
        // The online copy is gone: the join link no longer resolves.
        Assert.Null(await store.GetAsync(group.JoinToken, ParticipantProof.None));
        Assert.Empty(await store.ListOwnedAsync(accountId));
    }

    /// <summary>Creates a shared-by-link group owned by the signed-in account, for the deletion tests.</summary>
    private async Task<(Guid AccountId, MintedLinks Links)> OwnedSharedGroupFor(HttpClient client)
    {
        var profile = await (await client.GetAsync(new Uri("/api/v1/account/profile", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ProfileResponse>();

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISharedGroupStore>();

        using var body = JsonDocument.Parse("""{ "name": "Family" }""");
        var created = await store.CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, body.RootElement.Clone(), "creator-key"));
        var links = created.Links!;
        await store.ClaimAdminAsync(links.AdminToken, profile!.Id, profile.DisplayName);
        return (profile.Id, links);
    }

    [Fact]
    public async Task Delete_FreesTheAddressForAFreshSignup()
    {
        // ADR-0022: re-registering produces an entirely new account with nothing restored.
        var (email, client, _) = await SignedInAsync();

        await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(Password));

        using var anonymous = factory.CreateClient();
        var again = await anonymous.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Someone Else", email, Password, "en"));

        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        await DispatchOutboxAsync();

        // A verification mail means a genuinely new pending account, not a resurrected one.
        await factory.WaitForMailAsync(email, subjectContains: "Confirm your email address");
    }
}
