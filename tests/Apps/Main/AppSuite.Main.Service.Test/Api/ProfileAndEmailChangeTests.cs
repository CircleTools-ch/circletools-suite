using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppSuite.Infrastructure.Outbox;
using AppSuite.Main.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Covers profile editing (FR-012) and the verify-before-swap email change (FR-013).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class ProfileAndEmailChangeTests(MailApiFactory factory)
{
    private const string Password = "correct-horse-battery";

    private static string UniqueEmail() => $"profile-{Guid.CreateVersion7():N}@example.test";

    private async Task<int> DispatchOutboxAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxDispatcher>()
            .DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
    }

    private async Task<(string Email, HttpClient Client, Guid UserId)> SignedInAsync(string language = "en")
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, Password, language));
        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var (userId, token) = LinkParser.Parse(body);

        await client.PostAsJsonAsync("/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password));
        var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        return (email, client, Guid.Parse(userId));
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndLanguage()
    {
        var (_, client, _) = await SignedInAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/account/profile",
            new UpdateProfileRequest("Anna Müller", "fr-CH"));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Anna Müller", profile.DisplayName);
        Assert.Equal("fr-CH", profile.Language);

        // ...and it persisted, rather than just being echoed back.
        var reread = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/account/profile");
        Assert.Equal("Anna Müller", reread!.DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_EmptyDisplayName_IsRejected()
    {
        var (_, client, _) = await SignedInAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/account/profile",
            new UpdateProfileRequest("   ", "en"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_AllowsDuplicateDisplayNames()
    {
        // FR-012: display names are not unique — a constraint would force people to adopt names
        // nobody calls them.
        var (_, first, _) = await SignedInAsync();
        var (_, second, _) = await SignedInAsync();

        (await first.PutAsJsonAsync("/api/v1/account/profile", new UpdateProfileRequest("Anna", "en")))
            .EnsureSuccessStatusCode();
        (await second.PutAsJsonAsync("/api/v1/account/profile", new UpdateProfileRequest("Anna", "en")))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateProfile_RequiresAuthentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PutAsJsonAsync(
            "/api/v1/account/profile",
            new UpdateProfileRequest("Anna", "en"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeEmail_KeepsTheOldAddressWorkingUntilConfirmed()
    {
        var (oldEmail, client, _) = await SignedInAsync();
        var newEmail = UniqueEmail();

        var requested = await client.PostAsJsonAsync(
            "/api/v1/account/change-email", new ChangeEmailRequest(newEmail));
        requested.EnsureSuccessStatusCode();
        await DispatchOutboxAsync();

        // Both parties are told: the new address is asked to confirm, the old is warned.
        await factory.WaitForMailAsync(newEmail, subjectContains: "Confirm your new email address");
        await factory.WaitForMailAsync(oldEmail, subjectContains: "Email address change requested");

        // Until confirmation, the account is still reachable at the old address.
        using var anonymous = factory.CreateClient();
        var oldStillWorks = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(oldEmail, Password));
        oldStillWorks.EnsureSuccessStatusCode();

        var newNotYet = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(newEmail, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, newNotYet.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_MovesTheAccountToTheNewAddress()
    {
        var (oldEmail, client, userId) = await SignedInAsync();
        var newEmail = UniqueEmail();

        await client.PostAsJsonAsync("/api/v1/account/change-email", new ChangeEmailRequest(newEmail));
        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(newEmail, subjectContains: "Confirm your new email address");
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var token = ExtractToken(body);

        var confirm = await client.PostAsJsonAsync(
            "/api/v1/account/confirm-email-change",
            new ConfirmEmailChangeRequest(userId.ToString(), newEmail, token));
        confirm.EnsureSuccessStatusCode();

        using var anonymous = factory.CreateClient();

        var newWorks = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(newEmail, Password));
        newWorks.EnsureSuccessStatusCode();

        // The old address must stop resolving the account — UserName has to move with Email.
        var oldFails = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(oldEmail, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldFails.StatusCode);
    }

    [Fact]
    public async Task ChangeEmail_ToATakenAddress_IsIndistinguishableAndSendsNothing()
    {
        // NFR-005: an authenticated attacker must not be able to probe which addresses exist.
        var (_, victimClient, _) = await SignedInAsync();
        var (occupied, _, _) = await SignedInAsync();
        var free = UniqueEmail();

        var takenResponse = await victimClient.PostAsJsonAsync(
            "/api/v1/account/change-email", new ChangeEmailRequest(occupied));
        var freeResponse = await victimClient.PostAsJsonAsync(
            "/api/v1/account/change-email", new ChangeEmailRequest(free));

        Assert.Equal(freeResponse.StatusCode, takenResponse.StatusCode);
        Assert.Equal(
            await freeResponse.Content.ReadAsStringAsync(),
            await takenResponse.Content.ReadAsStringAsync());

        await DispatchOutboxAsync();

        // The free address is asked to confirm; the occupied one receives nothing new.
        await factory.WaitForMailAsync(free, subjectContains: "Confirm your new email address");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.WaitForMailAsync(occupied, subjectContains: "Confirm your new email address", attempts: 4));
    }

    [Fact]
    public async Task ConfirmEmailChange_ForAnotherUser_IsRefused()
    {
        var (_, client, _) = await SignedInAsync();
        var (_, _, otherUserId) = await SignedInAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/confirm-email-change",
            new ConfirmEmailChangeRequest(otherUserId.ToString(), UniqueEmail(), "whatever"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractToken(string body)
    {
        var start = body.IndexOf("http", StringComparison.Ordinal);
        var end = body.IndexOfAny(['\n', '\r'], start);
        var link = (end < 0 ? body[start..] : body[start..end]).Trim();
        var query = new Uri(link).Query.TrimStart('?');

        return query.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p[0] == "token")
            .Select(p => Uri.UnescapeDataString(p[1]))
            .Single();
    }
}
