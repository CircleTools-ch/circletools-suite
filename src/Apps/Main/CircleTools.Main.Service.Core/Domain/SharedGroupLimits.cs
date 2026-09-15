namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// The abuse-resistance limits for the account-free shared-group write path (NFR-009, ADR-0058).
/// Because the store keeps the body opaque, the caps are deliberately schema-light: a hard byte
/// ceiling on the stored document and a structural ceiling on the member roster (checked only when a
/// <c>members</c> array is present), which together bound storage and amplification without coupling
/// Platform to the Secret-Santa schema. Bound to the <c>SharedGroups</c> configuration section.
/// </summary>
public sealed class SharedGroupLimits
{
    /// <summary>The configuration section these limits bind to.</summary>
    public const string SectionName = "SharedGroups";

    /// <summary>The largest stored body, in bytes (UTF-8). Default 256 KiB.</summary>
    public int MaxBodyBytes { get; set; } = 256 * 1024;

    /// <summary>The most members a shared roster may carry, when a <c>members</c> array is present. Default 200.</summary>
    public int MaxMembers { get; set; } = 200;

    /// <summary>How long an inactive shared-by-link group is kept before the retention sweep removes it (NFR-010). Default 365 days.</summary>
    public int RetentionDays { get; set; } = 365;
}
