using CircleTools.SharedKernel;

namespace CircleTools.Identity.Tokens;

/// <summary>
/// A single sign-in session's succession of refresh tokens, and the rules that govern it
/// (FR-008, ADR-0021): every redemption rotates, the previous token is invalidated, and
/// replaying a consumed token is treated as theft.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity issues and accepts refresh tokens but does <em>not</em> track a
/// chain, enforce single use, or detect reuse — so this is deliberately hand-written, and is
/// the most security-sensitive logic in M1.
/// <para>
/// The type is pure: no EF, no ASP.NET, no clock and no randomness of its own. Callers supply
/// <c>now</c> and the new handle, which is what makes the expiry and racing cases testable
/// without waiting for real time to pass.
/// </para>
/// </remarks>
public sealed class RefreshTokenChain
{
    private readonly List<RefreshToken> _tokens = [];

    private RefreshTokenChain(Guid id, Guid userId)
    {
        Id = id;
        UserId = userId;
    }

    /// <summary>Gets the chain's identity.</summary>
    public Guid Id { get; }

    /// <summary>Gets the user this session belongs to.</summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the coarse device label recorded at sign-in ("Chrome · Windows"), for the list of signed-in
    /// devices (ADR-0079 amendment). Empty when unknown.
    /// </summary>
    public string Device { get; private set; } = string.Empty;

    /// <summary>Gets when the session started (its first token). By time, not list position: a store may load the tokens in any order.</summary>
    public DateTimeOffset SignedInAt => _tokens.Min(t => t.IssuedAt);

    /// <summary>Gets when the session was last used (its newest token).</summary>
    public DateTimeOffset LastActiveAt => _tokens.Max(t => t.IssuedAt);

    /// <summary>Gets when the session's newest token expires — after that nothing can refresh it.</summary>
    public DateTimeOffset ExpiresAt => _tokens.Max(t => t.ExpiresAt);

    /// <summary>The longest device label kept.</summary>
    public const int MaxDeviceLength = 100;

    /// <summary>Gets the moment the chain was revoked, or <c>null</c> while it is live.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Gets a value indicating whether the chain has been revoked.</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Gets the tokens issued in this chain, oldest first.</summary>
    public IReadOnlyList<RefreshToken> Tokens => _tokens;

    /// <summary>Gets the most recently issued token.</summary>
    public RefreshToken Current => _tokens[^1];

    /// <summary>Begins a new chain — one successful sign-in.</summary>
    /// <param name="userId">The user signing in.</param>
    /// <param name="handle">Stored handle of the first refresh token.</param>
    /// <param name="now">The current time.</param>
    /// <param name="options">Configured lifetimes.</param>
    /// <param name="device">The coarse device label to show in the list of signed-in devices, if known.</param>
    /// <returns>The new chain, holding its first token.</returns>
    public static RefreshTokenChain Start(
        Guid userId,
        string handle,
        DateTimeOffset now,
        TokenLifetimeOptions options,
        string? device = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentNullException.ThrowIfNull(options);

        var label = (device ?? string.Empty).Trim();
        var chain = new RefreshTokenChain(GuidV7.New(), userId)
        {
            Device = label.Length > MaxDeviceLength ? label[..MaxDeviceLength] : label,
        };
        chain._tokens.Add(new RefreshToken(handle, now, now + options.RefreshTokenLifetime));
        return chain;
    }

    /// <summary>Presents a refresh token for rotation.</summary>
    /// <param name="presentedHandle">The stored handle the client presented.</param>
    /// <param name="newHandle">Stored handle to issue if rotation succeeds.</param>
    /// <param name="newSecret">
    /// The client-facing secret matching <paramref name="newHandle"/>. Retained on the consumed
    /// predecessor for the replay grace window so a concurrent request can be answered with the
    /// same token, then forgotten.
    /// </param>
    /// <param name="now">The current time.</param>
    /// <param name="options">Configured lifetimes and the replay grace window.</param>
    /// <returns>
    /// <see cref="RefreshTokenRedemption.Rotated"/> on the normal path;
    /// <see cref="RefreshTokenRedemption.ReplayedWithinGrace"/> for a concurrent redemption;
    /// otherwise <see cref="RefreshTokenRedemption.Rejected"/>.
    /// </returns>
    /// <remarks>
    /// Detecting reuse <em>revokes the whole chain</em> as a side effect: once a token is known
    /// to have leaked, every descendant of it is suspect, so the session ends and the user signs
    /// in again.
    /// </remarks>
    public RefreshTokenRedemption Redeem(
        string presentedHandle,
        string newHandle,
        string newSecret,
        DateTimeOffset now,
        TokenLifetimeOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presentedHandle);
        ArgumentException.ThrowIfNullOrWhiteSpace(newHandle);
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecret);
        ArgumentNullException.ThrowIfNull(options);

        if (IsRevoked)
        {
            return new RefreshTokenRedemption.Rejected(RefreshTokenRejection.ChainRevoked);
        }

        ForgetSecretsOutsideGrace(now, options);

        var presented = _tokens.SingleOrDefault(t =>
            string.Equals(t.Handle, presentedHandle, StringComparison.Ordinal));

        if (presented is null)
        {
            return new RefreshTokenRedemption.Rejected(RefreshTokenRejection.UnknownToken);
        }

        if (!presented.IsConsumed)
        {
            if (presented.IsExpiredAt(now))
            {
                return new RefreshTokenRedemption.Rejected(RefreshTokenRejection.Expired);
            }

            var successor = new RefreshToken(newHandle, now, now + options.RefreshTokenLifetime);
            presented.Consume(now, successor.Handle, newSecret);
            _tokens.Add(successor);
            return new RefreshTokenRedemption.Rotated(successor);
        }

        // Already consumed. Either two tabs raced, or the token leaked and is being replayed.
        // We cannot tell the two apart from the token alone, so we separate them by time: a
        // replay within the grace window is answered idempotently with the successor that the
        // original redemption already produced.
        var withinGrace = now - presented.ConsumedAt!.Value <= options.RefreshReplayGraceWindow;
        var successorToken = _tokens.SingleOrDefault(t =>
            string.Equals(t.Handle, presented.SuccessorHandle, StringComparison.Ordinal));

        // The retained secret is what makes an idempotent answer possible at all. Once it has
        // been forgotten there is nothing to hand back, so the replay is indistinguishable from
        // theft — which is the correct reading, because forgetting only happens after the window.
        if (withinGrace
            && successorToken is not null
            && !successorToken.IsExpiredAt(now)
            && presented.SuccessorSecret is { } secret)
        {
            return new RefreshTokenRedemption.ReplayedWithinGrace(successorToken, secret);
        }

        Revoke(now);
        return new RefreshTokenRedemption.Rejected(RefreshTokenRejection.ReuseDetected);
    }

    /// <summary>
    /// Drops retained successor secrets whose replay window has closed, so the reversible copy
    /// exists for seconds rather than for the life of the session.
    /// </summary>
    private void ForgetSecretsOutsideGrace(DateTimeOffset now, TokenLifetimeOptions options)
    {
        foreach (var token in _tokens)
        {
            if (token.SuccessorSecret is not null
                && token.ConsumedAt is { } consumedAt
                && now - consumedAt > options.RefreshReplayGraceWindow)
            {
                token.ForgetSuccessorSecret();
            }
        }
    }

    /// <summary>
    /// Ends the session. Used by sign-out, by password reset (FR-009 revokes every session),
    /// by account deletion (FR-015), and by reuse detection above.
    /// </summary>
    /// <param name="now">The current time.</param>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
