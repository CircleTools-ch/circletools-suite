namespace CircleTools.SecretSanta.LocalStore;

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
/// The result of one <see cref="SyncService"/> cycle (FEAT-004): whether it completed, and what moved. The counts
/// are the evidence the status surface is built on — in particular <see cref="Collisions"/>, the bounded, counted
/// last-write-wins loss (ADR-0016).
/// </summary>
/// <param name="Status">How the cycle ended.</param>
/// <param name="Pushed">Locally-dirty documents sent to the server.</param>
/// <param name="Pulled">Documents changed elsewhere and applied locally.</param>
/// <param name="Collisions">Pushed bodies the server discarded under last-write-wins.</param>
public sealed record SyncOutcome(SyncStatus Status, int Pushed, int Pulled, int Collisions)
{
    /// <summary>Whether the cycle completed.</summary>
    public bool IsSuccess => Status == SyncStatus.Synced;

    /// <summary>A cycle that did not complete, carrying no counts.</summary>
    /// <param name="status">Why it did not complete.</param>
    /// <returns>The failed outcome.</returns>
    public static SyncOutcome Incomplete(SyncStatus status) => new(status, 0, 0, 0);
}
