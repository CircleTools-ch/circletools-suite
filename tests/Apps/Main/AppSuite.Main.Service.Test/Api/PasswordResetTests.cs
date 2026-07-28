using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppSuite.Infrastructure.Outbox;
using AppSuite.Main.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Covers password reset (FR-009) end to end, including the enumeration and session-revocation
/// rules that make it a genuine recovery path rather than just a convenience.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class PasswordResetTests(MailApiFactory factory)
{
    private const string OriginalPassword = "correct-horse-battery";
    private const string NewPassword = "another-long-passphrase";

    private static string UniqueEmail() => $"reset-{Guid.CreateVersion7():N}@example.test";

    private async Task<int> DispatchOutboxAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxDispatcher>()
            .DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
    }

    private async Task<(string Email, SessionResponse Session)> SignedInUserAsync(string language = "en")
    {
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, OriginalPassword, language));
        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var (userId, token) = LinkParser.Parse(body);

        await client.PostAsJsonAsync("/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(email, OriginalPassword));
        login.EnsureSuccessStatusCode();

        return (email, (await login.Content.ReadFromJsonAsync<SessionResponse>())!);
    }

    private async Task<(string UserId, string Token)> RequestResetAsync(string email)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password",
            new ForgotPasswordRequest(email));
        response.EnsureSuccessStatusCode();

        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(email, subjectContains: "Reset your password");
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        return LinkParser.Parse(body);
    }

    [Fact]
    public async Task ForgotPassword_UnknownAddress_LooksIdenticalAndSendsNothing()
    {
        using var client = factory.CreateClient();
        var (known, _) = await SignedInUserAsync();
        var unknown = UniqueEmail();

        var knownResponse = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password", new ForgotPasswordRequest(known));
        var unknownResponse = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password", new ForgotPasswordRequest(unknown));

        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(
            await knownResponse.Content.ReadAsStringAsync(),
            await unknownResponse.Content.ReadAsStringAsync());

        await DispatchOutboxAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.WaitForMailAsync(unknown, attempts: 4));
    }

    [Fact]
    public async Task Reset_ReplacesThePasswordAndRevokesEverySession()
    {
        var (email, session) = await SignedInUserAsync();
        var (userId, token) = await RequestResetAsync(email);

        using var client = factory.CreateClient();
        var reset = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password",
            new ResetPasswordRequest(userId, token, NewPassword));
        reset.EnsureSuccessStatusCode();

        // The session that existed before the reset is gone — this is what ejects an intruder.
        var refresh = await client.PostAsJsonAsync(
            "/api/v1/account/refresh", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // The old password no longer works; the new one does.
        var oldLogin = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, OriginalPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, NewPassword));
        newLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reset_LinkIsSingleUse()
    {
        var (email, _) = await SignedInUserAsync();
        var (userId, token) = await RequestResetAsync(email);

        using var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password", new ResetPasswordRequest(userId, token, NewPassword));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password",
            new ResetPasswordRequest(userId, token, "yet-another-passphrase"));

        Assert.NotEqual(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task RequestingReset_DoesNotInvalidateTheCurrentPassword()
    {
        // A stranger who types someone's address must not be able to lock them out.
        var (email, _) = await SignedInUserAsync();

        await RequestResetAsync(email);

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, OriginalPassword));

        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reset_ShortPassword_IsRejected()
    {
        var (email, _) = await SignedInUserAsync();
        var (userId, token) = await RequestResetAsync(email);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password", new ResetPasswordRequest(userId, token, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_BreachedPassword_IsRejectedAndTheOldPasswordStillWorks()
    {
        // FR-007's policy is a property of setting a password, not of the sign-up screen, so it
        // must hold on the recovery path too (ADR-0027). A rejected reset must also leave the
        // account usable rather than stranding someone mid-recovery.
        var (email, _) = await SignedInUserAsync();
        var (userId, token) = await RequestResetAsync(email);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password",
            new ResetPasswordRequest(userId, token, "iloveyouiloveyou"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors").GetProperty("password").EnumerateArray()
            .Select(e => e.GetString()).ToArray();

        Assert.Contains("BreachedPassword", errors);

        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, OriginalPassword));

        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reset_AlsoVerifiesAPendingAddress()
    {
        // Completing a reset proves control of the address, so it does the verification mail's job
        // (UC-MAIN-007 extension 2b).
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, OriginalPassword, "en"));

        var (userId, token) = await RequestResetAsync(email);

        var reset = await client.PostAsJsonAsync(
            "/api/v1/account/reset-password", new ResetPasswordRequest(userId, token, NewPassword));
        reset.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, NewPassword));
        login.EnsureSuccessStatusCode();

        var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.True(session.EmailVerified);
    }

    [Theory]
    [InlineData("de-CH", "Passwort zurücksetzen")]
    [InlineData("fr-CH", "Réinitialiser votre mot de passe")]
    public async Task ResetMail_UsesTheAccountsLanguage(string language, string subject)
    {
        // NFR-003: the mail follows the *account's* language, not the requesting browser's — the
        // requester may not be the owner.
        var (email, _) = await SignedInUserAsync(language);

        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/account/forgot-password", new ForgotPasswordRequest(email));
        await DispatchOutboxAsync();

        var mail = await factory.WaitForMailAsync(email, subjectContains: subject);

        Assert.Equal(subject, mail.GetProperty("Subject").GetString());
    }
}

/// <summary>Pulls the userId/token pair out of a link in a mail body.</summary>
internal static class LinkParser
{
    public static (string UserId, string Token) Parse(string body)
    {
        var start = body.IndexOf("http", StringComparison.Ordinal);
        var end = body.IndexOfAny(['\n', '\r'], start);
        var link = (end < 0 ? body[start..] : body[start..end]).Trim();
        var query = new Uri(link).Query.TrimStart('?');
        var parts = query.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);

        return (parts["userId"], parts["token"]);
    }
}
