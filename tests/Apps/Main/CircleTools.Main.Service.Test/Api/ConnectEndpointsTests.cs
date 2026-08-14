using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// End-to-end coverage of the cross-app SSO hand-off (FR-031, ADR-0039) through the real host: the
/// bearer-authorized <c>connect/authorize</c> mint and the anonymous <c>connect/token</c> redeem, with
/// the security envelope asserted — auth required, origin-bound, single-use, PKCE-bound — and
/// session-shape parity with a password sign-in.
/// </summary>
[Collection(AccountApi.CollectionName)]
public sealed class ConnectEndpointsTests(AccountApiFactory factory)
{
    private const string ValidPassword = "correct-horse-battery";
    private const string AllowedReturnUri = "https://sibling.test/account/connect/callback";
    private const string Verifier = "the-requesting-apps-in-memory-pkce-verifier-value";

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@example.test";

    private HttpClient Client() => factory.CreateClient();

    private static string Challenge(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static ConnectAuthorizeRequest AuthorizeRequest(string returnUri) =>
        new("secretsanta-web", returnUri, Challenge(Verifier), "S256");

    private async Task<List<CircleTools.Infrastructure.Outbox.OutboxMessage>> OutboxForAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var all = await context.OutboxMessages.AsNoTracking().ToListAsync();

        return [.. all.Where(m => m.Payload.Contains(email, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Registers, verifies and signs in, returning a live session on the authority.</summary>
    private async Task<SessionResponse> SignedInSessionAsync()
    {
        var email = UniqueEmail();
        var register = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "de-CH"));
        register.EnsureSuccessStatusCode();

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

        return (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private async Task<string> MintCodeAsync(string? returnUri = null)
    {
        var session = await SignedInSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/connect/authorize")
        {
            Content = JsonContent.Create(AuthorizeRequest(returnUri ?? AllowedReturnUri)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await Client().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<ConnectAuthorizeResponse>())!;
        return body.Code;
    }

    [Fact]
    public async Task ConnectAuthorize_WithoutABearerToken_IsUnauthorized()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/authorize",
            AuthorizeRequest(AllowedReturnUri));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConnectAuthorize_WithAReturnUriOffTheAllowList_IsBadRequest()
    {
        var session = await SignedInSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/connect/authorize")
        {
            Content = JsonContent.Create(AuthorizeRequest("https://evil.example/steal")),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await Client().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConnectFlow_MintThenRedeem_YieldsASession()
    {
        var code = await MintCodeAsync();

        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/token",
            new ConnectTokenRequest(code, Verifier));

        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<SessionResponse>())!;

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.True(session.ExpiresIn > 0);
    }

    [Fact]
    public async Task ConnectToken_ASecondRedeemOfTheSameCode_IsDenied()
    {
        var code = await MintCodeAsync();

        var first = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/token",
            new ConnectTokenRequest(code, Verifier));
        first.EnsureSuccessStatusCode();

        var second = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/token",
            new ConnectTokenRequest(code, Verifier));

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task ConnectToken_WithAWrongVerifier_IsDenied()
    {
        var code = await MintCodeAsync();

        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/token",
            new ConnectTokenRequest(code, "not-the-verifier"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConnectToken_TheBootstrappedRefreshToken_RefreshesThroughTheNormalPath()
    {
        var code = await MintCodeAsync();

        var token = await Client().PostAsJsonAsync(
            "/api/v1/account/connect/token",
            new ConnectTokenRequest(code, Verifier));
        token.EnsureSuccessStatusCode();
        var session = (await token.Content.ReadFromJsonAsync<SessionResponse>())!;

        // Parity with FR-008: the session an SSO hand-off produces is an ordinary session, so its
        // refresh token refreshes exactly like a password sign-in's.
        var refresh = await Client().PostAsJsonAsync(
            "/api/v1/account/refresh",
            new RefreshRequest(session.RefreshToken));

        refresh.EnsureSuccessStatusCode();
        var refreshed = (await refresh.Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(session.RefreshToken, refreshed.RefreshToken);
    }
}
