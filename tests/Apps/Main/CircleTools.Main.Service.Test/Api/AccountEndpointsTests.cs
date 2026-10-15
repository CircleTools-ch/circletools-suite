using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// End-to-end coverage of the account endpoints (FR-007, FR-008, NFR-005) through the real host.
/// </summary>
[Collection(AccountApi.CollectionName)]
public sealed class AccountEndpointsTests(AccountApiFactory factory)
{
    private static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@example.test";

    /// <summary>Strips the per-request correlation id so two problem responses can be compared.</summary>
    private static string WithoutTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");

    private const string ValidPassword = "correct-horse-battery";

    private HttpClient Client() => factory.CreateClient();

    private async Task<List<OutboxMessage>> OutboxForAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var all = await context.OutboxMessages.AsNoTracking().ToListAsync();

        return [.. all.Where(m => m.Payload.Contains(email, StringComparison.OrdinalIgnoreCase))];
    }

    private async Task<(HttpResponseMessage Response, string Email)> RegisterAsync(string? email = null)
    {
        var address = email ?? UniqueEmail();
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", address, ValidPassword, "de-CH"));

        return (response, address);
    }

    private async Task<SessionResponse> RegisterVerifyAndLoginAsync() =>
        (await RegisterVerifyAndLoginWithEmailAsync()).Session;

    private async Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        await Client().PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, password));

    private async Task<SessionResponse> SecondSessionAsync(string email)
    {
        var login = await LoginAsync(email, ValidPassword);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private HttpClient Authorized(SessionResponse session)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private async Task<HttpStatusCode> RefreshStatusAsync(SessionResponse session) =>
        (await Client().PostAsJsonAsync("/api/v1/account/refresh", new RefreshRequest(session.RefreshToken))).StatusCode;

    private async Task<(SessionResponse Session, string Email)> RegisterVerifyAndLoginWithEmailAsync()
    {
        var (_, email) = await RegisterAsync();
        var message = Assert.Single(await OutboxForAsync(email));
        var payload = JsonDocument.Parse(message.Payload).RootElement;

        var verify = await Client().PostAsJsonAsync(
            "/api/v1/account/verify-email",
            new VerifyEmailRequest(
                payload.GetProperty("userId").GetString()!,
                payload.GetProperty("token").GetString()!));
        verify.EnsureSuccessStatusCode();

        var login = await Client().PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(email, ValidPassword));
        login.EnsureSuccessStatusCode();

        return ((await login.Content.ReadFromJsonAsync<SessionResponse>())!, email);
    }

    [Fact]
    public async Task Register_CreatesPendingAccountAndQueuesVerificationMail()
    {
        var (response, email) = await RegisterAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var message = Assert.Single(await OutboxForAsync(email));
        Assert.Equal(AccountEndpoints.VerificationMailType, message.Type);
        Assert.Contains("token", message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_ExistingAddress_IsIndistinguishableButWarnsTheRealOwner()
    {
        // NFR-005: the requester learns nothing. The address owner is told someone tried.
        var (first, email) = await RegisterAsync();
        var (second, _) = await RegisterAsync(email);

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());

        var messages = await OutboxForAsync(email);
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Type == AccountEndpoints.VerificationMailType);
        Assert.Contains(messages, m => m.Type == AccountEndpoints.SignUpAttemptMailType);
    }

    [Fact]
    public async Task Register_ShortPassword_IsRejected()
    {
        // FR-007: 12-character minimum. This is the caller's own input, so reporting it leaks nothing.
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", UniqueEmail(), "short", "de-CH"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PasswordTooShort", await PasswordErrorsAsync(response));
    }

    [Fact]
    public async Task Register_BreachedPassword_IsRejected()
    {
        // FR-007 / ADR-0027: long enough to clear the length rule, and in the breach corpus.
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", UniqueEmail(), "passwordpassword", "de-CH"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("BreachedPassword", await PasswordErrorsAsync(response));
    }

    [Fact]
    public async Task Register_BreachedPassword_CreatesNoAccount()
    {
        var email = UniqueEmail();

        await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, "qwertyuiopasdfgh", "de-CH"));

        // The address must still be free — a rejected password may not leave a half-made account
        // behind, which would also make the address unusable for the person who owns it.
        var retry = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, "a-passphrase-nobody-leaked", "de-CH"));

        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
    }

    [Fact]
    public async Task PasswordRejections_AreReportedAsCodesNotEnglishProse()
    {
        // NFR-003: the client says this in DE/EN/FR, which it can only do from a code. Identity's
        // own Description is English, and shipping it would put untranslated text on screen.
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", UniqueEmail(), "short", "de-CH"));

        var errors = await PasswordErrorsAsync(response);

        Assert.All(errors, error => Assert.DoesNotContain(' ', error));
    }

    /// <summary>Reads the <c>password</c> field of a ValidationProblem body.</summary>
    private static async Task<string[]> PasswordErrorsAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. problem.GetProperty("errors").GetProperty("password").EnumerateArray()
            .Select(e => e.GetString()!)];
    }

    [Fact]
    public async Task Login_BeforeVerification_SucceedsButReportsUnverified()
    {
        // FR-007 (decided): a pending account may sign in; account-scoped features stay shut.
        var (_, email) = await RegisterAsync();

        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(email, ValidPassword));

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.False(session.EmailVerified);
        Assert.NotEmpty(session.AccessToken);
    }

    [Fact]
    public async Task VerifyEmail_MarksTheAccountVerified()
    {
        var session = await RegisterVerifyAndLoginAsync();

        Assert.True(session.EmailVerified);
    }

    [Fact]
    public async Task Login_UnknownAddressAndWrongPassword_AreIndistinguishable()
    {
        var (_, email) = await RegisterAsync();

        var wrongPassword = await Client().PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(email, "definitely-not-the-password"));
        var unknownAddress = await Client().PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(UniqueEmail(), ValidPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(unknownAddress.StatusCode, wrongPassword.StatusCode);

        // ProblemDetails carries a per-request traceId that differs on every call regardless of
        // the outcome, so it is not an enumeration signal. Everything else must match exactly.
        Assert.Equal(
            WithoutTraceId(await unknownAddress.Content.ReadAsStringAsync()),
            WithoutTraceId(await wrongPassword.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Refresh_RotatesTheTokenAndTheNewOneWorks()
    {
        var session = await RegisterVerifyAndLoginAsync();

        var refreshed = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh",
            new RefreshRequest(session.RefreshToken));
        refreshed.EnsureSuccessStatusCode();

        var next = await refreshed.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(next);
        Assert.NotEqual(session.RefreshToken, next.RefreshToken);
        Assert.NotEmpty(next.AccessToken);

        var again = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh",
            new RefreshRequest(next.RefreshToken));
        again.EnsureSuccessStatusCode();
    }

    // Theft detection is deliberately NOT asserted over HTTP. A test here runs in milliseconds,
    // well inside the 30-second replay grace window, so a replayed token correctly succeeds —
    // proving nothing about detection. Exercising it needs control of the clock, which the
    // service- and store-level tests have (RefreshTokenChainStoreTests, SessionServiceTests).

    [Fact]
    public async Task Refresh_ConcurrentCallers_BothSucceedWithTheSameToken()
    {
        // ADR-0021's racing-refresh case, over HTTP.
        var session = await RegisterVerifyAndLoginAsync();

        var first = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh", new RefreshRequest(session.RefreshToken));
        var second = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh", new RefreshRequest(session.RefreshToken));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        var a = await first.Content.ReadFromJsonAsync<SessionResponse>();
        var b = await second.Content.ReadFromJsonAsync<SessionResponse>();

        Assert.Equal(a!.RefreshToken, b!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_GarbageToken_IsRefused()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh",
            new RefreshRequest("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_RequiresAValidAccessToken()
    {
        var anonymous = await Client().GetAsync(new Uri("/api/v1/account/profile", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Profile_ReturnsTheSignedInUser()
    {
        var session = await RegisterVerifyAndLoginAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync(new Uri("/api/v1/account/profile", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Anna", profile.DisplayName);
        Assert.Equal("de-CH", profile.Language);
        Assert.True(profile.EmailVerified);
    }

    [Fact]
    public async Task Logout_RevokesTheSession()
    {
        var session = await RegisterVerifyAndLoginAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var logout = await client.PostAsync(new Uri("/api/v1/account/logout", UriKind.Relative), content: null);
        logout.EnsureSuccessStatusCode();

        var refresh = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh",
            new RefreshRequest(session.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithThisSessionsRefreshToken_EndsOnlyThisSession()
    {
        // ADR-0079: signing out on one device must not sign out the others.
        var (phone, email) = await RegisterVerifyAndLoginWithEmailAsync();
        var browser = await SecondSessionAsync(email);
        using var client = Authorized(phone);

        var logout = await client.PostAsJsonAsync("/api/v1/account/logout", new LogoutRequest(phone.RefreshToken));
        logout.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshStatusAsync(phone));
        Assert.Equal(HttpStatusCode.OK, await RefreshStatusAsync(browser));
    }

    [Fact]
    public async Task Logout_WithAnotherUsersRefreshToken_EndsNothingOfTheirs()
    {
        var mine = await RegisterVerifyAndLoginAsync();
        var theirs = await RegisterVerifyAndLoginAsync();
        using var client = Authorized(mine);

        var logout = await client.PostAsJsonAsync("/api/v1/account/logout", new LogoutRequest(theirs.RefreshToken));
        logout.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, await RefreshStatusAsync(theirs));
    }

    [Fact]
    public async Task LogoutAll_EndsEverySession()
    {
        var (phone, email) = await RegisterVerifyAndLoginWithEmailAsync();
        var browser = await SecondSessionAsync(email);
        using var client = Authorized(phone);

        var logout = await client.PostAsync(new Uri("/api/v1/account/logout-all", UriKind.Relative), content: null);
        logout.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshStatusAsync(phone));
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshStatusAsync(browser));
    }

    [Fact]
    public async Task ChangePassword_SetsTheNewPassword_KeepsThisSession_EndsTheOthers_AndSendsANotice()
    {
        var (phone, email) = await RegisterVerifyAndLoginWithEmailAsync();
        var browser = await SecondSessionAsync(email);
        using var client = Authorized(phone);
        const string newPassword = "a-brand-new-long-passphrase";

        var change = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(ValidPassword, newPassword, phone.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, ValidPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await RefreshStatusAsync(phone));
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshStatusAsync(browser));
        Assert.Contains(await OutboxForAsync(email), m => m.Type == AccountEndpoints.PasswordChangedMailType);
    }

    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_AnswersLikeAFailedSignIn_AndChangesNothing()
    {
        var (session, email) = await RegisterVerifyAndLoginWithEmailAsync();
        using var client = Authorized(session);

        var change = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest("not-the-password", "a-brand-new-long-passphrase", session.RefreshToken));
        var failedLogin = await LoginAsync(email, "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
        Assert.Equal(
            WithoutTraceId(await failedLogin.Content.ReadAsStringAsync()),
            WithoutTraceId(await change.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, ValidPassword)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ToOneThatBreaksThePolicy_IsRefused()
    {
        var (session, email) = await RegisterVerifyAndLoginWithEmailAsync();
        using var client = Authorized(session);

        var change = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(ValidPassword, "short", session.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, ValidPassword)).StatusCode);
    }
}
