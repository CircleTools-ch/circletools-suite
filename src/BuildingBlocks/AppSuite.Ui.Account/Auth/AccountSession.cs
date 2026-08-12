using AppSuite.ApiClient;
using AppSuite.Localization;
using AppSuite.Main.Contracts;
using AppSuite.Ui.Account.Api;
using AppSuite.Ui.Abstractions;

namespace AppSuite.Ui.Account.Auth;

/// <summary>
/// Starts, restores and ends the signed-in session — the one place that writes the token stores.
/// </summary>
/// <remarks>
/// Pages never touch <see cref="IAccessTokenStore"/> or <see cref="IRefreshTokenStore"/> themselves.
/// Every way in and out of a session has to do the same four things (store the tokens, follow the
/// account's language, tell the component tree, and leave nothing behind on the way out), and a page
/// that does three of them is a bug that only shows up on someone else's screen.
/// </remarks>
/// <param name="api">The account API.</param>
/// <param name="tokens">The in-memory access-token store.</param>
/// <param name="refreshTokens">Where the refresh token is remembered.</param>
/// <param name="refresher">The refresh implementation, reused for the startup restore.</param>
/// <param name="culture">The UI language, which follows the account's preference on sign-in.</param>
/// <param name="auth">The provider that publishes the current principal.</param>
public sealed class AccountSession(
    AccountApiClient api,
    IAccessTokenStore tokens,
    IRefreshTokenStore refreshTokens,
    ISessionRefresher refresher,
    ICultureProvider culture,
    TokenAuthStateProvider auth)
{
    /// <summary>
    /// Restores a session left behind by an earlier visit, if there is one.
    /// </summary>
    /// <remarks>
    /// Called once at startup, before the first render. The access token lives in memory only
    /// (ADR-0021), so a reload always arrives with nothing; the refresh token is what makes the
    /// session look continuous. When the API cannot be reached the app simply starts signed out and
    /// keeps the refresh token — it must not hang on a spinner or fail to start.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the restore.</param>
    /// <returns><see langword="true"/> if a session was recovered.</returns>
    public async ValueTask<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokens.ReadAsync(cancellationToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var accessToken = await refresher.RefreshAsync(cancellationToken);

        if (accessToken is null)
        {
            return false;
        }

        await ApplyAccountLanguageAsync(cancellationToken);
        return true;
    }

    /// <summary>Signs in with email and password (FR-008).</summary>
    /// <param name="email">The account's address.</param>
    /// <param name="password">The account's password.</param>
    /// <param name="staySignedIn">Whether the session should survive a browser restart (ADR-0026).</param>
    /// <param name="cancellationToken">Cancels the sign-in.</param>
    /// <returns>The session, or the failure to show.</returns>
    public async Task<ApiResult<SessionResponse>> SignInAsync(
        string email,
        string password,
        bool staySignedIn,
        CancellationToken cancellationToken = default)
    {
        var result = await api.LoginAsync(new LoginRequest(email, password), cancellationToken);
        return await AcceptAsync(result, staySignedIn, cancellationToken);
    }

    /// <summary>Signs in with a passkey assertion (FR-011).</summary>
    /// <param name="credentialJson">The assertion produced by the authenticator.</param>
    /// <param name="staySignedIn">Whether the session should survive a browser restart (ADR-0026).</param>
    /// <param name="cancellationToken">Cancels the sign-in.</param>
    /// <returns>The session, or the failure to show.</returns>
    public async Task<ApiResult<SessionResponse>> SignInWithPasskeyAsync(
        string credentialJson,
        bool staySignedIn,
        CancellationToken cancellationToken = default)
    {
        var result = await api.PasskeyLoginAsync(new PasskeyLoginRequest(credentialJson), cancellationToken);
        return await AcceptAsync(result, staySignedIn, cancellationToken);
    }

    /// <summary>
    /// Ends the session everywhere (FR-008).
    /// </summary>
    /// <remarks>
    /// The local tokens are cleared whatever the server says. A failed <c>/logout</c> — offline, or
    /// a token the server already rejected — must still leave the browser signed out; the opposite
    /// would strand a user on a shared machine with a live session they were told was closed.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the server call, not the local clear.</param>
    /// <returns>A task that completes once nothing of the session remains locally.</returns>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (tokens.AccessToken is not null)
        {
            await api.LogoutAsync(cancellationToken);
        }

        await ForgetAsync(cancellationToken);
    }

    /// <summary>
    /// Drops the local session without calling the server.
    /// </summary>
    /// <remarks>
    /// For the cases where the server has already ended it: a completed password reset revokes every
    /// session, and a deleted account has none left to revoke.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the clear.</param>
    /// <returns>A task that completes once the stores are empty.</returns>
    public async Task ForgetAsync(CancellationToken cancellationToken = default)
    {
        tokens.Clear();
        await refreshTokens.ClearAsync(cancellationToken);
        auth.NotifyChanged();
    }

    private async Task<ApiResult<SessionResponse>> AcceptAsync(
        ApiResult<SessionResponse> result,
        bool staySignedIn,
        CancellationToken cancellationToken)
    {
        if (result.Value is not { } session)
        {
            return result;
        }

        tokens.SetToken(session.AccessToken, TimeSpan.FromSeconds(session.ExpiresIn));
        await refreshTokens.SaveAsync(session.RefreshToken, staySignedIn, cancellationToken);
        auth.NotifyChanged();
        await ApplyAccountLanguageAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Switches the UI to the account's stored language.
    /// </summary>
    /// <remarks>
    /// The account's preference wins over whatever this browser last remembered: signing in on a
    /// borrowed device should not leave the user reading someone else's language (FR-012). The
    /// preference travels in the access token, so this costs no extra round trip.
    /// </remarks>
    private async ValueTask ApplyAccountLanguageAsync(CancellationToken cancellationToken)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var locale = state.User.Locale();

        if (!string.IsNullOrWhiteSpace(locale))
        {
            await culture.SetCultureAsync(AppCultures.Normalize(locale), cancellationToken);
        }
    }
}
