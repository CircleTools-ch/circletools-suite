namespace AppSuite.Identity.Tokens;

/// <summary>
/// One link in a <see cref="RefreshTokenChain"/>: a single issued refresh token.
/// </summary>
/// <remarks>
/// <see cref="Handle"/> is the <em>stored</em> handle — a hash of the secret handed to the
/// client, never the secret itself. Hashing happens outside the domain so this type stays
/// pure and testable.
/// </remarks>
public sealed class RefreshToken
{
    internal RefreshToken(string handle, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        Handle = handle;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the stored handle (a hash of the client's secret).</summary>
    public string Handle { get; }

    /// <summary>Gets the moment this token was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Gets the moment this token stops being valid.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the moment this token was redeemed, or <c>null</c> if it is still unused.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Gets the handle this token was rotated into, or <c>null</c> if it is still unused.</summary>
    public string? SuccessorHandle { get; private set; }

    /// <summary>
    /// Gets the successor's <em>secret</em>, retained only long enough to answer a concurrent
    /// replay, then cleared.
    /// </summary>
    /// <remarks>
    /// Every other secret is stored one-way as a <see cref="Handle"/>, which is what stops a
    /// database dump from yielding usable tokens. This one field is the deliberate exception:
    /// answering the racing-refresh case means handing the second caller the <em>same</em> token
    /// the first caller got, and a hash cannot be turned back into it.
    /// <para>
    /// The exposure is bounded to the replay grace window — seconds — after which
    /// <see cref="RefreshTokenChain.Redeem"/> clears it. A dump therefore yields only tokens
    /// issued in the last few seconds, rather than every live session.
    /// </para>
    /// </remarks>
    public string? SuccessorSecret { get; private set; }

    /// <summary>Gets a value indicating whether this token has already been redeemed.</summary>
    public bool IsConsumed => ConsumedAt is not null;

    /// <summary>Returns whether the token is expired at <paramref name="now"/>.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    internal void Consume(DateTimeOffset at, string successorHandle, string successorSecret)
    {
        ConsumedAt = at;
        SuccessorHandle = successorHandle;
        SuccessorSecret = successorSecret;
    }

    /// <summary>Drops the retained successor secret once the replay window has closed.</summary>
    internal void ForgetSuccessorSecret() => SuccessorSecret = null;
}
