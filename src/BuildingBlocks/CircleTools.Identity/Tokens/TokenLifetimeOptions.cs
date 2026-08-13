namespace CircleTools.Identity.Tokens;

/// <summary>
/// Session lifetimes for the bearer-token model (ADR-0021), pinned by FR-008.
/// </summary>
public sealed class TokenLifetimeOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Auth:TokenLifetimes";

    /// <summary>
    /// Gets or sets how long an access token is valid. Held in memory only, never in
    /// <c>localStorage</c> — ADR-0021 requires "minutes, not hours".
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets how long a refresh token is valid. Rotated on every use, forming a
    /// 30-day sliding window.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets how long an already-consumed refresh token may still be replayed and
    /// answered with the successor it already produced, instead of being treated as theft.
    /// </summary>
    /// <remarks>
    /// This exists for the racing-refresh case ADR-0021 calls out: two tabs holding the same
    /// refresh token may redeem it near-simultaneously, and that must not look like a stolen
    /// token.
    /// <para>
    /// It is a deliberate, bounded weakening of reuse detection: a token replayed by an
    /// <em>attacker</em> inside this window also receives the successor rather than tripping
    /// detection. Keep it in seconds — long enough to cover concurrent tabs, short enough that
    /// it is not a useful attack window.
    /// </para>
    /// </remarks>
    public TimeSpan RefreshReplayGraceWindow { get; set; } = TimeSpan.FromSeconds(30);
}
