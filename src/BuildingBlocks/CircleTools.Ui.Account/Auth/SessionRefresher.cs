using System.Net.Http.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.Ui.Account.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// Exchanges the stored refresh token for a new access token (ADR-0021).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately tiny and deliberately separate from <see cref="AccountSession"/>. It talks to the
/// API through the <see cref="AccountApiClients.Anonymous"/> client, because the refresh call is the
/// one request that must never pass through the handler that triggers refreshes.
/// </para>
/// <para>
/// A rejected refresh is the end of the session, not an error to retry: the server answers the same
/// way for an expired token and for one it believes was stolen (ADR-0021), and in both cases the
/// only correct client behaviour is to forget everything and let the user sign in again.
/// </para>
/// </remarks>
/// <param name="http">The unauthenticated client.</param>
/// <param name="tokens">The in-memory access-token store.</param>
/// <param name="refreshTokens">Where the refresh token is remembered.</param>
/// <param name="auth">The provider to notify when the session starts or ends.</param>
/// <param name="notices">Told when the server refused the session, so the shell can say the user was signed out.</param>
public sealed class SessionRefresher(
    [FromKeyedServices(AccountApiClients.Anonymous)] HttpClient http,
    IAccessTokenStore tokens,
    IRefreshTokenStore refreshTokens,
    TokenAuthStateProvider auth,
    SessionNotices notices) : ISessionRefresher
{
    /// <inheritdoc />
    public async ValueTask<string?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokens.ReadAsync(cancellationToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            await SignOutAsync(cancellationToken);
            return null;
        }

        SessionResponse? session;

        try
        {
            using var response = await http.PostAsJsonAsync(
                $"{AccountApiClients.AccountRoute}/refresh",
                new RefreshRequest(refreshToken),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The server ended this session: signed out on another device ("sign out on all devices"),
                // a password change elsewhere, or expiry. Say so instead of silently dropping the user out
                // (FR-008, ADR-0079).
                await SignOutAsync(cancellationToken);
                notices.RaiseSignedOutByServer();
                return null;
            }

            session = await response.Content.ReadFromJsonAsync<SessionResponse>(
                ApiResponseReader.JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Offline is not the same as signed out. Keeping the refresh token means the session
            // comes back when the network does, which is the whole point of a local-first app
            // (ADR-0021 calls this out: unreachable must degrade, not destroy).
            return null;
        }

        if (session is null)
        {
            await SignOutAsync(cancellationToken);
            return null;
        }

        tokens.SetToken(session.AccessToken, TimeSpan.FromSeconds(session.ExpiresIn));
        await refreshTokens.RenewAsync(session.RefreshToken, cancellationToken);
        auth.NotifyChanged();

        // Also covers a session started before the account was remembered with it (offline cold start).
        var accountId = (await auth.GetAuthenticationStateAsync()).User.UserId();
        if (accountId != Guid.Empty)
        {
            await refreshTokens.RememberAccountAsync(accountId, cancellationToken);
        }

        return session.AccessToken;
    }

    private async ValueTask SignOutAsync(CancellationToken cancellationToken)
    {
        tokens.Clear();
        await refreshTokens.ClearAsync(cancellationToken);
        auth.NotifyChanged();
    }
}
