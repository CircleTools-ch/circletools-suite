namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// The secrets that protect the shared-by-link store at rest (ADR-0061, ADR-0050). Bound from the
/// <c>SharedGroups:Security</c> configuration section; in production every value arrives from the host
/// environment, never from a checked-in file.
/// </summary>
public sealed class SharedGroupSecurityOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "SharedGroups:Security";

    /// <summary>
    /// Base64-encoded AES-256 key (32 bytes) that encrypts share tokens at rest, so a database dump on its
    /// own yields no working links (ADR-0061). Losing or rotating this key strands every already-shared
    /// link, so it is a durable, backed-up secret (ADR-0050).
    /// </summary>
    public string TokenEncryptionKey { get; set; } = string.Empty;
}
