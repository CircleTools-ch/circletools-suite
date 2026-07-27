namespace AppSuite.Identity.Tokens;

/// <summary>A freshly issued access token and the moment it stops being accepted.</summary>
/// <param name="Value">The encoded JWT.</param>
/// <param name="ExpiresAt">Expiry, so the client can refresh before rather than after a failure.</param>
public readonly record struct AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>The pair of tokens that make up a signed-in session (FR-008).</summary>
/// <param name="AccessToken">Short-lived; the client holds this in memory only.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires.</param>
/// <param name="RefreshSecret">
/// The refresh secret to hand to the client. Only its hash is stored server-side.
/// </param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires.</param>
public sealed record SessionTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshSecret,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>The outcome of a refresh attempt.</summary>
public abstract record SessionRefreshResult
{
    private SessionRefreshResult()
    {
    }

    /// <summary>The session continues, with a rotated refresh token.</summary>
    /// <param name="Tokens">The new token pair.</param>
    public sealed record Succeeded(SessionTokens Tokens) : SessionRefreshResult;

    /// <summary>The refresh was refused and the client must sign in again.</summary>
    /// <param name="Reason">Why it was refused.</param>
    public sealed record Failed(RefreshTokenRejection Reason) : SessionRefreshResult;
}
