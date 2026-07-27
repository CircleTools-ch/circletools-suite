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

    /// <summary>Gets a value indicating whether this token has already been redeemed.</summary>
    public bool IsConsumed => ConsumedAt is not null;

    /// <summary>Returns whether the token is expired at <paramref name="now"/>.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    internal void Consume(DateTimeOffset at, string successorHandle)
    {
        ConsumedAt = at;
        SuccessorHandle = successorHandle;
    }
}
