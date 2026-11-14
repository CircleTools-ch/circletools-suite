// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Identity.Tokens;

/// <summary>
/// A minted, not-yet-redeemed cross-app SSO authorization code (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// Only the <see cref="Handle"/> — the SHA-256 of the code the client holds — is ever stored, exactly
/// as refresh secrets are (<see cref="RefreshTokenSecret"/>). A dump of the store yields nothing
/// redeemable. The entry is single-use and short-lived: it is removed on redemption and ignored once
/// <see cref="ExpiresAt"/> has passed.
/// </remarks>
/// <param name="Handle">The SHA-256 handle of the code (the stored, non-secret form).</param>
/// <param name="UserId">The account the code will start a session for.</param>
/// <param name="Client">The requesting app identifier the code was minted for.</param>
/// <param name="ReturnUri">The redemption target the code was minted for.</param>
/// <param name="CodeChallenge">The PKCE challenge the redeemer must satisfy.</param>
/// <param name="CodeChallengeMethod">The PKCE method (only <c>S256</c>).</param>
/// <param name="ExpiresAt">When the code stops being redeemable.</param>
public sealed record ConnectAuthorizationCode(
    string Handle,
    Guid UserId,
    string Client,
    string ReturnUri,
    string CodeChallenge,
    string CodeChallengeMethod,
    DateTimeOffset ExpiresAt);
