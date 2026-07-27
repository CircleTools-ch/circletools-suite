namespace AppSuite.Identity.Tokens;

/// <summary>Signing and validation parameters for access tokens (ADR-0021).</summary>
public sealed class JwtOptions
{
    /// <summary>Minimum signing-key length in bytes, as required by HMAC-SHA256.</summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Auth:Jwt";

    /// <summary>Gets or sets the token issuer.</summary>
    public string Issuer { get; set; } = "appsuite";

    /// <summary>Gets or sets the intended audience — the suite API.</summary>
    public string Audience { get; set; } = "appsuite-api";

    /// <summary>
    /// Gets or sets the symmetric signing key. Supplied from configuration or user-secrets and
    /// <strong>never</strong> committed.
    /// </summary>
    /// <remarks>
    /// Must be at least <see cref="MinimumSigningKeyBytes"/> bytes. A short key would make the
    /// signature forgeable, which in a bearer-token model means minting arbitrary sessions —
    /// so it is validated at startup rather than trusted.
    /// </remarks>
    public string SigningKey { get; set; } = string.Empty;
}
