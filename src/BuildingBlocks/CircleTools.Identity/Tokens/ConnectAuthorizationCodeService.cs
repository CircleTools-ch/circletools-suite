// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Identity.Tokens;

/// <summary>
/// Mints and redeems cross-app SSO authorization codes (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The single place the code's security envelope lives: it is single-use (the store consumes it
/// atomically), short-lived (<see cref="CodeLifetime"/>), stored only as a SHA-256 handle, and bound
/// to the redeemer by PKCE. Redemption yields only a user id — turning that into a session is
/// <see cref="SessionService"/>'s job, so there is exactly one path that issues tokens.
/// </remarks>
/// <param name="store">Where minted codes live until consumed or expired.</param>
/// <param name="clock">Time source, injected so expiry is testable.</param>
public sealed class ConnectAuthorizationCodeService(IConnectAuthorizationCodeStore store, TimeProvider clock)
{
    /// <summary>
    /// How long a minted code stays redeemable — long enough for one redirect round-trip, no longer.
    /// </summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(30);

    /// <summary>Mints a single-use code for a signed-in user's sibling-app hand-off.</summary>
    /// <param name="userId">The authenticated user the code will start a session for.</param>
    /// <param name="client">The requesting app identifier.</param>
    /// <param name="returnUri">The redemption target (already validated against the allow-list).</param>
    /// <param name="codeChallenge">The PKCE challenge the redeemer must satisfy.</param>
    /// <param name="codeChallengeMethod">The PKCE method (only <c>S256</c> is accepted at redemption).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The plaintext code to carry back to the requesting app.</returns>
    public async Task<string> IssueAsync(
        Guid userId,
        string client,
        string returnUri,
        string codeChallenge,
        string codeChallengeMethod,
        CancellationToken cancellationToken = default)
    {
        var (secret, handle) = RefreshTokenSecret.Create();

        await store.AddAsync(
            new ConnectAuthorizationCode(
                handle,
                userId,
                client,
                returnUri,
                codeChallenge,
                codeChallengeMethod,
                clock.GetUtcNow() + CodeLifetime),
            cancellationToken).ConfigureAwait(false);

        return secret;
    }

    /// <summary>
    /// Consumes a code and, if it is valid, unexpired and PKCE-proven, returns the bound user id.
    /// </summary>
    /// <remarks>
    /// Every failure returns the same <see langword="null"/>: an unknown, already-consumed, expired or
    /// PKCE-mismatched code are indistinguishable to the caller, which keeps the endpoint's response
    /// uniform. The code is consumed even when the PKCE check then fails, so a leaked code cannot be
    /// brute-forced against by re-presenting it.
    /// </remarks>
    /// <param name="code">The code the requesting app received.</param>
    /// <param name="codeVerifier">The PKCE verifier proving it requested the code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user id to start a session for, or <see langword="null"/> on any failure.</returns>
    public async Task<Guid?> RedeemAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var entry = await store.ConsumeAsync(RefreshTokenSecret.HandleFor(code), cancellationToken).ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        if (entry.ExpiresAt <= clock.GetUtcNow())
        {
            return null;
        }

        return Pkce.Verify(codeVerifier, entry.CodeChallenge, entry.CodeChallengeMethod)
            ? entry.UserId
            : null;
    }
}
