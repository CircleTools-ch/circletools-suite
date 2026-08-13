namespace CircleTools.Identity.Tokens;

/// <summary>Why a refresh-token redemption was refused.</summary>
public enum RefreshTokenRejection
{
    /// <summary>The handle belongs to no token in this chain.</summary>
    UnknownToken,

    /// <summary>The token was valid but has passed its expiry.</summary>
    Expired,

    /// <summary>The chain was already revoked — by sign-out, password reset, or detected theft.</summary>
    ChainRevoked,

    /// <summary>
    /// A consumed token was replayed outside the grace window. Treated as theft:
    /// the whole chain is revoked (ADR-0021).
    /// </summary>
    ReuseDetected,
}

/// <summary>
/// The outcome of presenting a refresh token. See
/// <see cref="RefreshTokenChain.Redeem(string, string, string, DateTimeOffset, TokenLifetimeOptions)"/>.
/// </summary>
public abstract record RefreshTokenRedemption
{
    private RefreshTokenRedemption()
    {
    }

    /// <summary>The token was unused and has been rotated into a fresh successor.</summary>
    /// <param name="Token">The newly issued refresh token.</param>
    public sealed record Rotated(RefreshToken Token) : RefreshTokenRedemption;

    /// <summary>
    /// The token had already been consumed, but within the replay grace window, so the
    /// successor it previously produced is returned again rather than treating this as theft.
    /// </summary>
    /// <param name="Token">The successor issued by the original redemption.</param>
    /// <param name="Secret">
    /// That successor's secret, so the caller can be handed the <em>same</em> token as the
    /// request that raced it. Callers must return this rather than minting a new one — issuing a
    /// different token here would leave two live leaves on one chain, and the loser's next
    /// refresh would trip theft detection.
    /// </param>
    /// <remarks>
    /// This is the racing-refresh case: two browser tabs redeeming the same token at nearly
    /// the same moment must both end up signed in, holding the same token afterwards.
    /// </remarks>
    public sealed record ReplayedWithinGrace(RefreshToken Token, string Secret) : RefreshTokenRedemption;

    /// <summary>The redemption was refused.</summary>
    /// <param name="Reason">Why it was refused.</param>
    public sealed record Rejected(RefreshTokenRejection Reason) : RefreshTokenRedemption;
}
