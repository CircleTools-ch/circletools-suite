using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore;

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

    /// <summary>The per-account Secret-Santa UI preferences document (pinned groups). See ADR-0041.</summary>
    public const string Preferences = "preferences";
}

/// <summary>
/// The pristine, syncable body of the per-account Secret-Santa UI preferences document (ADR-0041):
/// the groups the user has pinned for quick access. Like <see cref="GroupState"/> it carries no sync
/// metadata — the version, account scope and dirty flag wrap it in <see cref="SyncedPreferencesRecord"/>
/// in the local store, and only this body crosses the wire as the opaque aggregate the server echoes to
/// the account's other devices.
/// </summary>
/// <param name="PinnedGroupIds">The pinned group ids, in the order the user pinned them.</param>
public sealed record PreferencesState(IReadOnlyList<Guid> PinnedGroupIds);

/// <summary>
/// The per-account UI preferences together with the sync metadata the client needs to reconcile it
/// (ADR-0041), mirroring <see cref="SyncedGroupRecord"/>. Preferences are a device-agnostic account
/// preference, so — unlike a group — the document exists only while signed in and is always scoped to an
/// account; there is no account-free preferences document.
/// </summary>
/// <param name="State">The pristine preferences body.</param>
/// <param name="Version">The per-document version, bumped on every local edit — the client's half of last-write-wins.</param>
/// <param name="AccountScope">The account these preferences belong to; also the aggregate id under which the document syncs.</param>
/// <param name="Dirty">Whether the local copy holds an edit not yet accepted by the server.</param>
public sealed record SyncedPreferencesRecord(
    PreferencesState State,
    long Version,
    Guid AccountScope,
    bool Dirty);

/// <summary>
/// Where a group's data lives — the per-group storage mode a user chooses (ADR-0058, FEAT-013).
/// Client-side state: like the account scope it wraps the domain memento in
/// <see cref="SyncedGroupRecord"/> and is never part of the <see cref="GroupState"/> the domain
/// produces (the <c>SyncLayeringTests</c> fitness function keeps sync/storage metadata out of the
/// memento).
/// </summary>
public enum StorageMode
{
    /// <summary>Local-only, offline, account-free — the default. Sharing is per-person reveal links only (NFR-001).</summary>
    OnThisDevice = 0,

    /// <summary>Stored on the server keyed by a share token, no account; reachable by an admin or join link (ADR-0058).</summary>
    SharedByLink = 1,

    /// <summary>Synced to the signed-in account across that user's devices.</summary>
    Account = 2,
}

/// <summary>
/// The share tokens this device holds for a <see cref="StorageMode.SharedByLink"/> group (ADR-0058):
/// the admin-capability token (present only for a holder of the admin link) and/or the join token.
/// Client-side metadata carried on <see cref="SyncedGroupRecord"/> — never in the domain memento.
/// </summary>
/// <param name="AdminToken">The admin-capability token, or <see langword="null"/> if this device holds only the join link.</param>
/// <param name="JoinToken">The join (participant) token, or <see langword="null"/>.</param>
public sealed record SharedGroupTokens(string? AdminToken, string? JoinToken);

/// <summary>
/// A persisted group together with the sync metadata the client needs to reconcile it (FEAT-004).
/// </summary>
/// <remarks>
/// The domain memento — <see cref="GroupState"/> — is kept pristine on purpose: the
/// <see cref="CircleTools.SecretSanta.Core.Domain.Group"/> aggregate gains nothing from sync, so the
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
/// <param name="Mode">
/// The group's storage mode (ADR-0058): on this device (default), shared by link, or account-synced.
/// Additive since schema v3; an older record without it loads as <see cref="StorageMode.OnThisDevice"/>.
/// </param>
/// <param name="Tokens">
/// For a <see cref="StorageMode.SharedByLink"/> group, the share tokens this device holds; otherwise
/// <see langword="null"/>. Additive since schema v3.
/// </param>
public sealed record SyncedGroupRecord(
    GroupState State,
    long Version,
    Guid? AccountScope,
    bool Dirty,
    StorageMode Mode = StorageMode.OnThisDevice,
    SharedGroupTokens? Tokens = null);

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
    /// (no wrapper, no sync metadata); version 2 wrapped each in a <see cref="SyncedGroupRecord"/>;
    /// version 3 adds the per-group <see cref="StorageMode"/> and its share tokens (ADR-0058) as
    /// additive record fields — a v2 document loads unchanged, every group defaulting to
    /// <see cref="StorageMode.OnThisDevice"/> with no tokens. A load also recognises the v1 array and
    /// lifts each element — no user loses a group.
    /// </summary>
    public const int CurrentSchemaVersion = 3;
}

/// <summary>
/// The account's profile preferences held locally, so an edit made offline survives a reload and can
/// be pushed on reconnect (FEAT-004, ADR-0035). Unlike a group this is not a sync-store aggregate: the
/// authoritative profile lives on the Main service's <c>/account/profile</c> endpoint (which drives the
/// JWT locale and the Main app), and <see cref="Dirty"/> marks an offline edit waiting to be
/// <c>PUT</c>-flushed there on the next sync trigger.
/// </summary>
/// <remarks>
/// There is exactly one profile per account. A different account signing in on the same device gets its
/// own record — the <see cref="AccountScope"/> guards a pending edit from being flushed to the wrong
/// account (FR-024). When no offline edit is pending the record simply is not stored; its presence with
/// <see cref="Dirty"/> set is the queued change FR-012 asks for.
/// </remarks>
/// <param name="AccountScope">The account these preferences belong to.</param>
/// <param name="DisplayName">The name others sharing a circle see.</param>
/// <param name="Language">The preferred language (<c>de-CH</c>, <c>en</c>, <c>fr-CH</c>).</param>
/// <param name="Dirty">Whether this holds an offline edit not yet accepted by the server.</param>
public sealed record SyncedProfileRecord(
    Guid AccountScope,
    string DisplayName,
    string Language,
    bool Dirty);

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
