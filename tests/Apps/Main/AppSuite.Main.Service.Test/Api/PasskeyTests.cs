using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppSuite.Identity;
using AppSuite.Infrastructure.Outbox;
using AppSuite.Main.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Covers the storage and management half of FR-010/FR-011.
/// </summary>
/// <remarks>
/// <strong>The WebAuthn ceremonies themselves are not exercised here.</strong> Attestation and
/// assertion need a browser with a virtual authenticator (TP-002), and the cross-client criterion
/// additionally needs the real domain, which [ADR-0023] records as unverifiable locally. What is
/// testable without a browser — that credentials are stored, listed, scoped to their owner,
/// removable, and that removing the last one leaves password sign-in working — is covered here so
/// those rules are not left entirely unguarded.
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class PasskeyTests(MailApiFactory factory)
{
    private const string Password = "correct-horse-battery";

    private static string UniqueEmail() => $"passkey-{Guid.CreateVersion7():N}@example.test";

    private async Task<(AppSuiteUser User, HttpClient Client, string Email)> SignedInAsync()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, Password, "en"));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>()
                .DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
        }

        var mail = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var (userId, token) = LinkParser.Parse(body);
        await client.PostAsJsonAsync("/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        await using var lookup = factory.Services.CreateAsyncScope();
        var users = lookup.ServiceProvider.GetRequiredService<UserManager<AppSuiteUser>>();
        var user = (await users.FindByIdAsync(userId))!;

        return (user, client, email);
    }

    /// <summary>Stores a credential directly, standing in for a completed attestation ceremony.</summary>
    private async Task<string> AddPasskeyAsync(AppSuiteUser user, string label)
    {
        var credentialId = Guid.CreateVersion7().ToByteArray();

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppSuiteUser>>();
        var tracked = (await users.FindByIdAsync(user.Id.ToString()))!;

        var passkey = new UserPasskeyInfo(
            credentialId,
            publicKey: [1, 2, 3, 4],
            createdAt: DateTimeOffset.UtcNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: false,
            attestationObject: [5, 6],
            clientDataJson: [7, 8])
        {
            Name = label,
        };

        var result = await users.AddOrUpdatePasskeyAsync(tracked, passkey);
        Assert.True(result.Succeeded);

        return WebEncoders.Base64UrlEncode(credentialId);
    }

    [Fact]
    public async Task Passkeys_AreListedWithTheirLabels()
    {
        var (user, client, _) = await SignedInAsync();
        await AddPasskeyAsync(user, "Phone");
        await AddPasskeyAsync(user, "Work laptop");

        var passkeys = await client.GetFromJsonAsync<List<PasskeyResponse>>("/api/v1/account/passkeys");

        Assert.NotNull(passkeys);
        Assert.Equal(2, passkeys.Count);
        Assert.Contains(passkeys, p => p.Label == "Phone");
        Assert.Contains(passkeys, p => p.Label == "Work laptop");
    }

    [Fact]
    public async Task Passkeys_AreScopedToTheirOwner()
    {
        var (owner, _, _) = await SignedInAsync();
        await AddPasskeyAsync(owner, "Owner's phone");
        var (_, stranger, _) = await SignedInAsync();

        var passkeys = await stranger.GetFromJsonAsync<List<PasskeyResponse>>("/api/v1/account/passkeys");

        Assert.NotNull(passkeys);
        Assert.Empty(passkeys);
    }

    [Fact]
    public async Task RemovingAPasskey_LeavesPasswordSignInWorking()
    {
        // FR-010: the password credential always remains, so removing the last passkey can never
        // lock anyone out.
        var (user, client, email) = await SignedInAsync();
        var credentialId = await AddPasskeyAsync(user, "Only device");

        var removed = await client.DeleteAsync(
            new Uri($"/api/v1/account/passkeys/{credentialId}", UriKind.Relative));
        removed.EnsureSuccessStatusCode();

        var remaining = await client.GetFromJsonAsync<List<PasskeyResponse>>("/api/v1/account/passkeys");
        Assert.Empty(remaining!);

        using var anonymous = factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RemovingAnotherUsersPasskey_IsRefused()
    {
        var (owner, _, _) = await SignedInAsync();
        var credentialId = await AddPasskeyAsync(owner, "Owner's phone");
        var (_, stranger, _) = await SignedInAsync();

        var response = await stranger.DeleteAsync(
            new Uri($"/api/v1/account/passkeys/{credentialId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // ...and it really is still there for its owner.
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppSuiteUser>>();
        var tracked = (await users.FindByIdAsync(owner.Id.ToString()))!;
        Assert.Single(await users.GetPasskeysAsync(tracked));
    }

    [Fact]
    public async Task PasskeyEndpoints_RequireAuthentication()
    {
        using var anonymous = factory.CreateClient();

        var list = await anonymous.GetAsync(new Uri("/api/v1/account/passkeys", UriKind.Relative));
        var options = await anonymous.PostAsync(
            new Uri("/api/v1/account/passkeys/options", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, options.StatusCode);
    }

    [Fact]
    public async Task CreationOptions_AreIssuedForASignedInUser()
    {
        var (_, client, email) = await SignedInAsync();

        var response = await client.PostAsync(
            new Uri("/api/v1/account/passkeys/options", UriKind.Relative), content: null);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // The options must name this user and carry a challenge; the browser does the rest.
        Assert.Contains(email, json, StringComparison.Ordinal);
        Assert.Contains("challenge", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestOptions_NameNoUser_SoTheyRevealNothing()
    {
        // NFR-005: sign-in options must not disclose which addresses are registered, so they are
        // issued for any caller and rely on discoverable credentials.
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsync(
            new Uri("/api/v1/account/login/passkey/options", UriKind.Relative), content: null);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("challenge", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasskeyLogin_WithGarbage_IsRefused()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login/passkey",
            new PasskeyLoginRequest("{\"not\":\"a credential\"}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
