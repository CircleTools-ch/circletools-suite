namespace AppSuite.ApiClient;

/// <summary>
/// Holds the current access token.
/// </summary>
/// <remarks>
/// <b>In memory only</b> (ADR-0021). The access token is never written to any browser storage: it
/// is the most trivially harvestable copy, and a reload can always mint a new one from the refresh
/// token. Implementations that persist it violate the decision.
/// </remarks>
public interface IAccessTokenStore
{
    /// <summary>The current access token, or <see langword="null"/> when signed out.</summary>
    string? AccessToken { get; }

    /// <summary>When the current access token expires.</summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>Whether there is no usable token, or it expires within <paramref name="margin"/>.</summary>
    /// <param name="margin">How far ahead to look, so a token does not expire mid-flight.</param>
    /// <returns><see langword="true"/> if the token should be refreshed before use.</returns>
    bool NeedsRefresh(TimeSpan margin);

    /// <summary>Replaces the current token.</summary>
    /// <param name="accessToken">The new access token.</param>
    /// <param name="expiresIn">How long it is valid for, as reported by the server.</param>
    void SetToken(string accessToken, TimeSpan expiresIn);

    /// <summary>Forgets the current token.</summary>
    void Clear();
}
