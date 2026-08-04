using AppSuite.SecretSanta.Core.Persistence;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// The aggregate kinds that cross the M3 sync wire (FEAT-004). Platform stores every kind as an
/// opaque document, so the string is the only thing that distinguishes a group from a profile on
/// either side (ADR-0016, ADR-0033).
/// </summary>
public static class SyncKinds
{
    /// <summary>A Secret-Santa group aggregate.</summary>
    public const string Group = "group";

    /// <summary>The per-account profile document (display name, preferred language).</summary>
    public const string Profile = "profile";
}

/// <summary>
/// A persisted group together with the sync metadata the client needs to reconcile it (FEAT-004).
/// </summary>
/// <remarks>
/// The domain memento — <see cref="GroupState"/> — is kept pristine on purpose: the
/// <see cref="AppSuite.SecretSanta.Core.Domain.Group"/> aggregate gains nothing from sync, so the
/// version, the account scope and the dirty flag wrap it here in the LocalStore adapter rather than
/// living inside the memento the domain produces (ADR-0016; the <c>SyncLayeringTests</c> fitness
/// function makes this a build-time rule).
/// </remarks>
/// <param name="State">The pristine domain memento.</param>
/// <param name="Version">
/// The per-aggregate version, bumped on every local edit. It is the client's half of last-write-wins:
/// the number the server compares against what it holds.
/// </param>
/// <param name="AccountScope">
/// The account this group is claimed into, or <see langword="null"/> for an account-free, local-only
/// group. Only a scoped group is ever pushed, and only to the account whose id this is — a group
/// claimed to one account is never sent to another that later signs in on the same device (FR-024).
/// </param>
/// <param name="Dirty">
/// Whether the local copy holds an edit not yet accepted by the server. Set on every local write,
/// cleared once a cycle has pushed the copy the flag was set for.
/// </param>
public sealed record SyncedGroupRecord(
    GroupState State,
    long Version,
    Guid? AccountScope,
    bool Dirty);

/// <summary>
/// The whole persisted group collection under the local-store key, carrying a schema version so an
/// older on-disk shape can be recognised and migrated in place (FEAT-004).
/// </summary>
/// <param name="SchemaVersion">The storage schema version this document was written with.</param>
/// <param name="Records">The groups and their sync metadata.</param>
public sealed record SyncGroupDocument(int SchemaVersion, IReadOnlyList<SyncedGroupRecord> Records)
{
    /// <summary>
    /// The current storage schema version. Version 1 was the bare <c>List&lt;GroupState&gt;</c> array
    /// (no wrapper, no sync metadata); version 2 is this document. A load recognises the old array and
    /// lifts each element into a <see cref="SyncedGroupRecord"/> — no user loses a group.
    /// </summary>
    public const int CurrentSchemaVersion = 2;
}

/// <summary>
/// A deletion made locally and not yet pushed (FEAT-004). Travels to the server as a tombstone so the
/// delete propagates to the account's other devices; it carries no body.
/// </summary>
/// <param name="AccountId">
/// The account the deleted group was claimed into. A pending tombstone is only ever pushed to this
/// account — the same cross-account rule the dirty groups follow (FR-024).
/// </param>
/// <param name="Kind">The aggregate kind the deletion applies to.</param>
/// <param name="Id">The deleted aggregate's identity.</param>
/// <param name="Version">The version at deletion, so a later higher-version re-creation is not re-deleted.</param>
public sealed record PendingTombstone(Guid AccountId, string Kind, Guid Id, long Version);

/// <summary>The cursor a device has reached on one account — the highest server sequence it has applied.</summary>
/// <param name="AccountId">The account this cursor belongs to.</param>
/// <param name="Cursor">The highest per-account sequence already applied on this device.</param>
public sealed record AccountSyncCursor(Guid AccountId, long Cursor);

/// <summary>
/// The device-level sync state: the stable device id, a per-account cursor, and the deletions still
/// waiting to be pushed (FEAT-004). Persisted alongside the groups but under its own key.
/// </summary>
/// <param name="DeviceId">
/// This device's stable identifier, minted once and kept across accounts. It distinguishes this
/// device's cursor and writes from the account's other devices, and drives tombstone retention.
/// </param>
/// <param name="Cursors">The cursor reached on each account this device has synced.</param>
/// <param name="Tombstones">Deletions queued for their next push, each bound to its account.</param>
public sealed record SyncState(
    string DeviceId,
    IReadOnlyList<AccountSyncCursor> Cursors,
    IReadOnlyList<PendingTombstone> Tombstones)
{
    /// <summary>The cursor reached on <paramref name="accountId"/>, or zero if never synced there.</summary>
    /// <param name="accountId">The account whose cursor is wanted.</param>
    /// <returns>The highest applied sequence for that account, or zero.</returns>
    public long CursorFor(Guid accountId) =>
        Cursors.FirstOrDefault(c => c.AccountId == accountId)?.Cursor ?? 0;
}
