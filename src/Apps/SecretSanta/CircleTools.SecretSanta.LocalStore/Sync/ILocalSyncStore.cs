namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The low-level persistence gateway the M3 client sync engine reads and writes through (FEAT-004):
/// the group collection with its sync metadata, and the device-level sync state.
/// </summary>
/// <remarks>
/// This sits under both the <see cref="SyncingGroupRepository"/> (which serves the domain port) and
/// the <see cref="SyncService"/> (which runs the cycle), so the two share one on-disk representation
/// and one migration. A write that the storage refuses is raised as a
/// <see cref="CircleTools.Ui.Abstractions.StorageUnavailableException"/> rather than swallowed: the local
/// store holds the only copy of an unsynced edit (NFR-001), and a lost pending tombstone would let a
/// deleted group come back.
/// </remarks>
public interface ILocalSyncStore
{
    /// <summary>
    /// Loads every group with its sync metadata, migrating an older on-disk shape in memory as it
    /// reads (no group is lost). The migrated shape is not written back until the next save.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored groups, or an empty list.</returns>
    Task<IReadOnlyList<SyncedGroupRecord>> LoadGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the whole group collection.</summary>
    /// <param name="records">The groups to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the write has stuck.</returns>
    Task SaveGroupsAsync(IReadOnlyList<SyncedGroupRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the device sync state, minting a fresh state with a new device id the first time it is
    /// asked for (so a device always has a stable id before its first cycle).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored or freshly-minted sync state.</returns>
    Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the device sync state (cursor, pending tombstones, device id).</summary>
    /// <param name="state">The state to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the write has stuck.</returns>
    Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the locally-held profile document (display name, preferred language and sync metadata), or
    /// <see langword="null"/> if none has been stored on this device yet (FEAT-004, ADR-0035).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored profile document, or <see langword="null"/>.</returns>
    Task<SyncedProfileRecord?> LoadProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the profile document (FEAT-004, ADR-0035).</summary>
    /// <param name="profile">The profile document to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the write has stuck.</returns>
    Task SaveProfileAsync(SyncedProfileRecord profile, CancellationToken cancellationToken = default);
}
