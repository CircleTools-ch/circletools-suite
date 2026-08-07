namespace AppSuite.SecretSanta.LocalStore;

/// <summary>How a sync cycle ended, as a fact the UI can phrase without it reading as an error (FEAT-004).</summary>
public enum SyncStatus
{
    /// <summary>The cycle completed: everything dirty was pushed and the server delta applied.</summary>
    Synced,

    /// <summary>The server could not be reached. The local copy is untouched and still usable (NFR-001).</summary>
    Offline,

    /// <summary>The session was refused. The user is effectively signed out; no local data is removed.</summary>
    SignedOut,

    /// <summary>Local storage refused a write, so the cycle was not committed and the cursor did not advance.</summary>
    StorageUnavailable,

    /// <summary>The cycle failed for another reason; nothing local was changed.</summary>
    Failed,
}

/// <summary>
/// The result of one <see cref="SyncService"/> cycle (FEAT-004): whether it completed, and what moved.
/// The counts are the evidence the parked conflict UI and the status surface are built on — in
/// particular <see cref="Collisions"/>, the bounded, counted last-write-wins loss (ADR-0016).
/// </summary>
/// <param name="Status">How the cycle ended.</param>
/// <param name="Pushed">Locally-dirty aggregates sent to the server.</param>
/// <param name="Pulled">Aggregates created or updated elsewhere and applied locally.</param>
/// <param name="Deleted">Tombstones applied locally — groups deleted elsewhere.</param>
/// <param name="Unclaimed">Groups removed from the account elsewhere; the local copy was kept, scope dropped.</param>
/// <param name="Collisions">Pushed bodies the server discarded under last-write-wins.</param>
public sealed record SyncOutcome(
    SyncStatus Status,
    int Pushed,
    int Pulled,
    int Deleted,
    int Unclaimed,
    int Collisions)
{
    /// <summary>Whether the cycle completed.</summary>
    public bool IsSuccess => Status == SyncStatus.Synced;

    /// <summary>A cycle that did not complete, carrying no counts.</summary>
    /// <param name="status">Why it did not complete.</param>
    /// <returns>The failed outcome.</returns>
    public static SyncOutcome Incomplete(SyncStatus status) => new(status, 0, 0, 0, 0, 0);
}

/// <summary>
/// The result of a <see cref="SyncService.ClaimAsync"/> call (FR-025): how far a per-group claim got.
/// Claiming is group-by-group so a partly-failed multi-group claim leaves the groups that arrived
/// account-scoped and the rest purely local — <see cref="Claimed"/> is exactly the arrived count, which
/// is what a retry then skips.
/// </summary>
/// <param name="Status">How the claim ended — <see cref="SyncStatus.Synced"/> if every requested group was claimed.</param>
/// <param name="Claimed">How many groups were uploaded and marked account-scoped.</param>
/// <param name="Requested">How many groups the caller asked to claim.</param>
public sealed record ClaimOutcome(SyncStatus Status, int Claimed, int Requested)
{
    /// <summary>Whether every requested group was claimed.</summary>
    public bool IsSuccess => Status == SyncStatus.Synced;

    /// <summary>A claim that stopped before finishing, keeping the count that did arrive.</summary>
    /// <param name="status">Why it stopped.</param>
    /// <param name="claimed">How many groups had already been claimed when it stopped.</param>
    /// <param name="requested">How many groups the caller asked to claim.</param>
    /// <returns>The incomplete outcome.</returns>
    public static ClaimOutcome Incomplete(SyncStatus status, int claimed, int requested) =>
        new(status, claimed, requested);
}
