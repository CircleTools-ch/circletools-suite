namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// The secrets that protect the Circles context (ADR-0062, ADR-0050). Bound from the
/// <c>Circles:Security</c> configuration section; in production the value arrives from the host
/// environment, never from a checked-in file.
/// </summary>
public sealed class CircleSecurityOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Circles:Security";

    /// <summary>
    /// Base64-encoded key for the HMAC-SHA256 that reduces an email address to its stored hash
    /// (ADR-0062). Stable for the life of the data: changing it orphans every stored hash, breaking
    /// decline-cooldown matching and invitation lookups, so it is a durable, backed-up secret (ADR-0050).
    /// </summary>
    public string EmailHashKey { get; set; } = string.Empty;
}
