using CircleTools.Main.Contracts;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Api;
using Microsoft.AspNetCore.Components;

namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// Drives the cross-app single-sign-on hand-off from the client (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// <para>
/// One service, three moments of the same flow — because the block runs on every origin, the code for
/// the requesting app and the authority app is the same code, entered at different routes:
/// </para>
/// <list type="bullet">
///   <item><see cref="BeginAsync"/> — the <em>requesting</em> app, with no session, sends the user to
///   the authority.</item>
///   <item><see cref="CompleteAuthorizeAsync"/> — the <em>authority</em> app, already signed in, mints
///   a code and sends the user back.</item>
///   <item><see cref="CompleteCallbackAsync"/> — the <em>requesting</em> app redeems the code for its
///   own session.</item>
/// </list>
/// <para>
/// The PKCE verifier is the one piece of state that must survive the redirect to the authority and
/// back, so it lives in <see cref="StorageScope.Session"/> storage between <see cref="BeginAsync"/>
/// and <see cref="CompleteCallbackAsync"/> and is removed the moment it is used — it must never
/// outlive the hand-off (its whole point is to be secret and short-lived).
/// </para>
/// </remarks>
/// <param name="api">The account API client (authorized for authorize, anonymous for token).</param>
/// <param name="session">The one place a session is written; the callback adopts through it.</param>
/// <param name="store">Session-scoped storage for the transient PKCE verifier.</param>
/// <param name="navigation">The navigation manager used for the top-level redirects.</param>
/// <param name="options">The authority origin and this app's client id.</param>
public sealed class SsoConnect(
    AccountApiClient api,
    AccountSession session,
    IKeyValueStore store,
    NavigationManager navigation,
    SsoConnectOptions options)
{
    private const string VerifierKey = "circletools.sso.verifier";
    private const string PendingReturnKey = "circletools.sso.pending.return";
    private const string PendingChallengeKey = "circletools.sso.pending.challenge";
    private const string PendingMethodKey = "circletools.sso.pending.method";
    private const string CallbackPath = "account/connect/callback";
    private const string AuthorizePath = "account/connect";

    /// <summary>An authorize request parked while the authority signs the user in.</summary>
    /// <param name="ReturnUri">The requesting app's callback URI.</param>
    /// <param name="Challenge">The PKCE challenge.</param>
    /// <param name="Method">The PKCE method.</param>
    public sealed record PendingAuthorize(string ReturnUri, string Challenge, string Method);

    /// <summary>
    /// Whether this app <em>is</em> the SSO authority (its origin matches the configured authority).
    /// </summary>
    /// <remarks>
    /// The authority never hands off to itself, so the interactive affordance suppresses itself here.
    /// This lets the shared login screen carry the affordance unconditionally: it shows on requesting
    /// apps and vanishes on the authority.
    /// </remarks>
    /// <returns><see langword="true"/> if this origin is the authority origin.</returns>
    public bool IsAuthority() =>
        Uri.TryCreate(options.AuthorityBaseUrl, UriKind.Absolute, out var authority)
        && Uri.TryCreate(navigation.BaseUri, UriKind.Absolute, out var here)
        && string.Equals(
            authority.GetLeftPart(UriPartial.Authority),
            here.GetLeftPart(UriPartial.Authority),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Starts the hand-off from a requesting app: generate PKCE, stash the verifier, and redirect to
    /// the authority. This is a top-level cross-origin navigation, so it does not return.
    /// </summary>
    /// <param name="cancellationToken">Cancels the verifier write (not the navigation).</param>
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        var (verifier, challenge) = PkceCodes.Create();
        await store.SetAsync(StorageScope.Session, VerifierKey, verifier, cancellationToken);

        var returnUri = navigation.ToAbsoluteUri(CallbackPath).ToString();
        var authorizeUrl =
            $"{options.AuthorityBaseUrl.TrimEnd('/')}/{AuthorizePath}"
            + $"?return_uri={Uri.EscapeDataString(returnUri)}"
            + $"&challenge={Uri.EscapeDataString(challenge)}"
            + $"&method={PkceCodes.Method}";

        navigation.NavigateTo(authorizeUrl, forceLoad: true);
    }

    /// <summary>
    /// On the authority (with a live session), mints a code for the requesting app and redirects back
    /// to its return URI carrying the code. Returns <see langword="false"/> if no code could be minted
    /// (offline, or the return URI was refused), so the caller can fall back rather than hang.
    /// </summary>
    /// <param name="returnUri">The requesting app's callback URI, from the incoming query.</param>
    /// <param name="challenge">The PKCE challenge, from the incoming query.</param>
    /// <param name="method">The PKCE method, from the incoming query.</param>
    /// <param name="cancellationToken">Cancels the mint call.</param>
    /// <returns><see langword="true"/> if it redirected back with a code.</returns>
    public async Task<bool> CompleteAuthorizeAsync(
        string returnUri,
        string challenge,
        string method,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(returnUri) || string.IsNullOrWhiteSpace(challenge))
        {
            return false;
        }

        var result = await api.ConnectAuthorizeAsync(
            new ConnectAuthorizeRequest(options.ClientId, returnUri, challenge, method),
            cancellationToken);

        if (result.Value is not { Code: var code } || string.IsNullOrEmpty(code))
        {
            return false;
        }

        var separator = returnUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        navigation.NavigateTo($"{returnUri}{separator}code={Uri.EscapeDataString(code)}", forceLoad: true);
        return true;
    }

    /// <summary>
    /// On the requesting app, redeems the returned code for this app's own session and adopts it.
    /// The verifier is consumed whatever the outcome, so a failed exchange cannot be retried against a
    /// stale secret.
    /// </summary>
    /// <param name="code">The authorization code, from the callback query.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns><see langword="true"/> if a session was started.</returns>
    public async Task<bool> CompleteCallbackAsync(string? code, CancellationToken cancellationToken = default)
    {
        var verifier = await store.GetAsync(StorageScope.Session, VerifierKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Session, VerifierKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(verifier))
        {
            return false;
        }

        var result = await api.ConnectTokenAsync(new ConnectTokenRequest(code, verifier), cancellationToken);

        if (result.Value is not { } adopted)
        {
            return false;
        }

        // Persist across tabs (ADR-0026 durable scope): a hand-off exists to spare the user a second
        // sign-in, so a session it bootstraps must survive a new tab the same way — otherwise every new
        // tab of the sibling would need the affordance clicked again, which defeats the point.
        await session.AdoptAsync(adopted, staySignedIn: true, cancellationToken);
        return true;
    }

    /// <summary>
    /// Parks an authorize request while the authority signs the user in, so it survives the round-trip
    /// through the login page (the request cannot ride the login <c>returnUrl</c> without tripping its
    /// open-redirect guard, so it waits in session storage instead).
    /// </summary>
    /// <param name="returnUri">The requesting app's callback URI.</param>
    /// <param name="challenge">The PKCE challenge.</param>
    /// <param name="method">The PKCE method.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    public async Task StashPendingAsync(
        string returnUri,
        string challenge,
        string method,
        CancellationToken cancellationToken = default)
    {
        await store.SetAsync(StorageScope.Session, PendingReturnKey, returnUri, cancellationToken);
        await store.SetAsync(StorageScope.Session, PendingChallengeKey, challenge, cancellationToken);
        await store.SetAsync(StorageScope.Session, PendingMethodKey, method, cancellationToken);
    }

    /// <summary>Reads a parked authorize request, or <see langword="null"/> if none is parked.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The parked request, or <see langword="null"/>.</returns>
    public async Task<PendingAuthorize?> ReadPendingAsync(CancellationToken cancellationToken = default)
    {
        var returnUri = await store.GetAsync(StorageScope.Session, PendingReturnKey, cancellationToken);
        var challenge = await store.GetAsync(StorageScope.Session, PendingChallengeKey, cancellationToken);
        var method = await store.GetAsync(StorageScope.Session, PendingMethodKey, cancellationToken);

        return string.IsNullOrWhiteSpace(returnUri) || string.IsNullOrWhiteSpace(challenge)
            ? null
            : new PendingAuthorize(returnUri, challenge, string.IsNullOrWhiteSpace(method) ? PkceCodes.Method : method);
    }

    /// <summary>Clears any parked authorize request.</summary>
    /// <param name="cancellationToken">Cancels the removals.</param>
    public async Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
        await store.RemoveAsync(StorageScope.Session, PendingReturnKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Session, PendingChallengeKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Session, PendingMethodKey, cancellationToken);
    }
}
