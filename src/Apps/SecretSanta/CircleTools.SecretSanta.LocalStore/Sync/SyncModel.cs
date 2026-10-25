namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The aggregate kinds that cross the account sync wire (FEAT-004). Platform stores every kind as an opaque
/// document, so the string is the only thing that distinguishes them (ADR-0016, ADR-0033). Groups do not travel
/// here: online groups live in the Secret-Santa group store (ADR-0086).
/// </summary>
public static class SyncKinds
{
    /// <summary>The per-account profile document (display name, preferred language).</summary>
    public const string Profile = "profile";

    /// <summary>The per-account Secret-Santa UI preferences document (pinned groups). See ADR-0041.</summary>
    public const string Preferences = "preferences";
}

/// <summary>
/// The pristine, syncable body of the per-account Secret-Santa UI preferences document (ADR-0041):
/// the groups the user has pinned for quick access. It carries no sync metadata — the version, account
/// scope and dirty flag wrap it in <see cref="SyncedPreferencesRecord"/>
/// in the local store, and only this body crosses the wire as the opaque aggregate the server echoes to
/// the account's other devices.
/// </summary>
/// <param name="PinnedGroupIds">The pinned group ids, in the order the user pinned them.</param>
public sealed record PreferencesState(IReadOnlyList<Guid> PinnedGroupIds);

/// <summary>
/// The per-account UI preferences together with the sync metadata the client needs to reconcile it
/// (ADR-0041). Preferences are a device-agnostic account preference, so the document exists only while
/// signed in and is always scoped to an account; there is no account-free preferences document.
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
/// The account's profile preferences held locally, so an edit made offline survives a reload and can
/// be pushed on reconnect (FEAT-004, ADR-0035). This is not a sync-store aggregate: the
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

/// <summary>The cursor a device has reached on one account — the highest server sequence it has applied.</summary>
/// <param name="AccountId">The account this cursor belongs to.</param>
/// <param name="Cursor">The highest per-account sequence already applied on this device.</param>
public sealed record AccountSyncCursor(Guid AccountId, long Cursor);

/// <summary>
/// The device-level sync state: the stable device id and a per-account cursor (FEAT-004). Persisted under its
/// own key. An older document's group fields (pending tombstones, retractions, owned deletes) are ignored on
/// load: groups left the account sync (ADR-0086).
/// </summary>
/// <param name="DeviceId">
/// This device's stable identifier, minted once and kept across accounts. It distinguishes this
/// device's cursor and writes from the account's other devices, and drives tombstone retention.
/// </param>
/// <param name="Cursors">The cursor reached on each account this device has synced.</param>
public sealed record SyncState(
    string DeviceId,
    IReadOnlyList<AccountSyncCursor> Cursors)
{
    /// <summary>The cursor reached on <paramref name="accountId"/>, or zero if never synced there.</summary>
    /// <param name="accountId">The account whose cursor is wanted.</param>
    /// <returns>The highest applied sequence for that account, or zero.</returns>
    public long CursorFor(Guid accountId) =>
        Cursors.FirstOrDefault(c => c.AccountId == accountId)?.Cursor ?? 0;
}
