namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The low-level persistence gateway the account sync reads and writes through (FEAT-004): the per-account
/// profile and preferences documents and the device-level sync state. Groups are kept elsewhere (ADR-0086).
/// </summary>
/// <remarks>
/// A write that the storage refuses is raised as a <see cref="CircleTools.Ui.Abstractions.StorageUnavailableException"/>
/// rather than swallowed: the local store holds the only copy of an unsynced edit (NFR-001).
/// </remarks>
public interface ILocalSyncStore
{
    /// <summary>
    /// Loads the device sync state, minting a fresh state with a new device id the first time it is
    /// asked for (so a device always has a stable id before its first cycle).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored or freshly-minted sync state.</returns>
    Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the device sync state (device id, cursors).</summary>
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

    /// <summary>
    /// Loads the locally-held Secret-Santa UI preferences document (pinned groups and sync metadata), or
    /// <see langword="null"/> if none has been stored on this device yet (ADR-0041).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored preferences document, or <see langword="null"/>.</returns>
    Task<SyncedPreferencesRecord?> LoadPreferencesAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the Secret-Santa UI preferences document (ADR-0041).</summary>
    /// <param name="preferences">The preferences document to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the write has stuck.</returns>
    Task SavePreferencesAsync(SyncedPreferencesRecord preferences, CancellationToken cancellationToken = default);
}
