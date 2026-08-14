namespace CircleTools.Identity.Tokens;

/// <summary>
/// Stores minted SSO authorization codes until they are redeemed once or expire (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The contract is deliberately small: add a code, and consume it <em>atomically</em> so a racing
/// double-redemption cannot both win. Single-use is the security property, and it lives here.
/// </remarks>
public interface IConnectAuthorizationCodeStore
{
    /// <summary>Stores a newly minted code.</summary>
    /// <param name="code">The code entry (only its handle is secret-derived).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AddAsync(ConnectAuthorizationCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and returns the code for a handle, if one is present — the single-use consume.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the handle is unknown or already consumed. The caller still
    /// checks expiry and PKCE; this method only guarantees a code is handed out <em>at most once</em>.
    /// </remarks>
    /// <param name="handle">The SHA-256 handle of the presented code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The consumed code, or <see langword="null"/>.</returns>
    ValueTask<ConnectAuthorizationCode?> ConsumeAsync(string handle, CancellationToken cancellationToken = default);
}
