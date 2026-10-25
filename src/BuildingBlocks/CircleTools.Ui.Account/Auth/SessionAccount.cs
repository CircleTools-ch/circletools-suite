using CircleTools.ApiClient;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// The account whose data this device shows: the signed-in account, or — while a remembered session waits for
/// the server to renew it — the account that session belongs to.
/// </summary>
/// <remarks>
/// The access token lives in memory only (ADR-0021). After a cold start without a reachable server the app has a
/// refresh token but no token to read a principal from, so it looks signed out until the refresh succeeds. Data
/// kept on the device for that account must not vanish meanwhile (NFR-001): offline is not signed out. The
/// remembered id is display state, never a credential — every request still needs a renewed token.
/// </remarks>
/// <param name="tokens">The in-memory access-token store.</param>
/// <param name="refreshTokens">Where the refresh token, and the account it belongs to, are remembered.</param>
/// <param name="auth">The provider that publishes the current principal.</param>
public sealed class SessionAccount(IAccessTokenStore tokens, IRefreshTokenStore refreshTokens, AuthenticationStateProvider auth)
{
    /// <summary>The account whose data to show, or <see langword="null"/> when there is none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The signed-in account, the account of a session waiting to be renewed, or <see langword="null"/>.</returns>
    public async ValueTask<Guid?> CurrentAsync(CancellationToken cancellationToken = default)
    {
        var signedIn = (await auth.GetAuthenticationStateAsync()).User.UserId();
        if (signedIn != Guid.Empty)
        {
            return signedIn;
        }

        // A token the principal could not be read from is not a waiting session; nothing to show.
        return tokens.AccessToken is null ? await refreshTokens.ReadAccountAsync(cancellationToken) : null;
    }
}
