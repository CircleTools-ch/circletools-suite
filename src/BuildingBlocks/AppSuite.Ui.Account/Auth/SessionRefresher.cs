using System.Net.Http.Json;
using AppSuite.ApiClient;
using AppSuite.Main.Contracts;
using AppSuite.Ui.Account.Api;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Ui.Account.Auth;

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
public sealed class SessionRefresher(
    [FromKeyedServices(AccountApiClients.Anonymous)] HttpClient http,
    IAccessTokenStore tokens,
    IRefreshTokenStore refreshTokens,
    TokenAuthStateProvider auth) : ISessionRefresher
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
                await SignOutAsync(cancellationToken);
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

        return session.AccessToken;
    }

    private async ValueTask SignOutAsync(CancellationToken cancellationToken)
    {
        tokens.Clear();
        await refreshTokens.ClearAsync(cancellationToken);
        auth.NotifyChanged();
    }
}
