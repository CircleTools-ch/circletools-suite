using CircleTools.ApiClient;
using CircleTools.Localization;
using CircleTools.Main.Contracts;
using CircleTools.Ui.Account.Api;
using CircleTools.Ui.Abstractions;

namespace CircleTools.Ui.Account.Auth;

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

        // Not the account language again: it was applied when this device signed in, and since then the
        // person may have chosen another language for this device in Settings ("only on this device"),
        // which must survive a reload (2026-10-16 test). Signing in still applies it (FR-012).
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
    /// Ends this device's session (FR-008, ADR-0079). The account's other devices stay signed in.
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
            var refreshToken = await refreshTokens.ReadAsync(cancellationToken);
            await api.LogoutAsync(refreshToken, cancellationToken);
        }

        await ForgetAsync(cancellationToken);
    }

    /// <summary>
    /// Ends every session of the account, on every device, including this one (FR-049, ADR-0079).
    /// </summary>
    /// <param name="cancellationToken">Cancels the server call, not the local clear.</param>
    /// <returns>The server's answer; this device is signed out either way.</returns>
    public async Task<ApiResult> SignOutEverywhereAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.LogoutAllAsync(cancellationToken);
        await ForgetAsync(cancellationToken);
        return result;
    }

    /// <summary>The account's signed-in devices, this one marked (ADR-0079 amendment).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sessions, or the failure.</returns>
    public async Task<ApiResult<List<SessionInfoResponse>>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokens.ReadAsync(cancellationToken);
        return await api.ListSessionsAsync(refreshToken, cancellationToken);
    }

    /// <summary>Signs out one other device (ADR-0079 amendment).</summary>
    /// <param name="sessionId">The session to end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> SignOutDeviceAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        api.RevokeSessionAsync(sessionId, cancellationToken);

    /// <summary>
    /// Changes the password (FR-049, ADR-0079). This device stays signed in; every other session ends.
    /// </summary>
    /// <param name="currentPassword">The password the account has now.</param>
    /// <param name="newPassword">The replacement password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome: a wrong current password answers like a failed sign-in.</returns>
    public async Task<ApiResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokens.ReadAsync(cancellationToken);
        return await api.ChangePasswordAsync(
            new ChangePasswordRequest(currentPassword, newPassword, refreshToken), cancellationToken);
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

    /// <summary>
    /// Adopts a session obtained outside this class's own API calls — the cross-app SSO hand-off
    /// (FR-031, ADR-0039).
    /// </summary>
    /// <remarks>
    /// The SSO token exchange returns the same <see cref="SessionResponse"/> a sign-in does, but the
    /// call is made by the connect flow, not here. Routing it through this method keeps the invariant
    /// that <see cref="AccountSession"/> is the one place the token stores are written — the four steps
    /// are the same as any other way into a session.
    /// </remarks>
    /// <param name="session">The session returned by the token exchange.</param>
    /// <param name="staySignedIn">Whether the session should survive a browser restart (ADR-0026).</param>
    /// <param name="cancellationToken">Cancels the adoption.</param>
    /// <returns>A task that completes once the session is in effect.</returns>
    public async Task AdoptAsync(
        SessionResponse session,
        bool staySignedIn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        tokens.SetToken(session.AccessToken, TimeSpan.FromSeconds(session.ExpiresIn));
        await refreshTokens.SaveAsync(session.RefreshToken, staySignedIn, cancellationToken);
        auth.NotifyChanged();
        await ApplyAccountLanguageAsync(cancellationToken);
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

        await AdoptAsync(session, staySignedIn, cancellationToken);
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
