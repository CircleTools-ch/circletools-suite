namespace AppSuite.SharedKernel;

/// <summary>
/// Helper for generating time-ordered GUID version 7 identifiers (ADR-0008).
/// UUID v7 keys are globally unique, offline-generatable, and index-friendly.
/// </summary>
public static class GuidV7
{
    /// <summary>Creates a new time-ordered <see cref="Guid"/> (UUID version 7).</summary>
    /// <returns>A new UUID v7 value.</returns>
    public static Guid New() => Guid.CreateVersion7();
}
