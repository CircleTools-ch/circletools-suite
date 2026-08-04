using System.Text.Json;

namespace AppSuite.Main.Contracts;

/// <summary>
/// One delta exchange for the M3 sync cycle (FEAT-004, <c>POST /api/v1/sync</c>). The client sends
/// what changed on its device since its cursor; the server replies with everything that changed on
/// the account above that cursor.
/// </summary>
/// <param name="DeviceId">
/// The calling device's stable identifier. Distinguishes this device's cursor and its own writes
/// from those of the account's other devices; used for tombstone/marker retention.
/// </param>
/// <param name="Cursor">
/// The highest per-account server sequence this device has already applied. The response returns
/// everything above it. Zero on a device that has never synced this account.
/// </param>
/// <param name="Changes">
/// The locally-dirty aggregates, each carrying its full body and version. Empty on a no-op cycle —
/// a device with nothing pending sends no bodies (NFR-008).
/// </param>
/// <param name="Tombstones">Deletions made locally since the last cycle, as id + version, no body.</param>
public sealed record SyncRequest(
    string DeviceId,
    long Cursor,
    IReadOnlyList<SyncChange> Changes,
    IReadOnlyList<SyncTombstone> Tombstones);

/// <summary>
/// The server's reply to a <see cref="SyncRequest"/>: everything on the account with a server
/// sequence above the request's cursor, plus the new cursor to advance to.
/// </summary>
/// <param name="Cursor">
/// The new cursor — the highest sequence the server assigned or holds. The client stores it and
/// sends it back next cycle.
/// </param>
/// <param name="Changes">Aggregates created or updated on other devices, each with its full body.</param>
/// <param name="Tombstones">Deletions to apply locally (id + version, no body).</param>
/// <param name="Unclaimed">
/// Groups removed from the account elsewhere. The device drops the account scope but keeps its local
/// copy — an un-claim marker is not a tombstone (FR-026).
/// </param>
/// <param name="Collisions">
/// One descriptor per aggregate whose pushed body lost last-write-wins and was discarded in full
/// (ADR-0016). Kind and id only — never content (ADR-0012/0020). The client is not asked to resolve
/// them in M3; the count is the evidence the parked conflict UI waits on.
/// </param>
public sealed record SyncResponse(
    long Cursor,
    IReadOnlyList<SyncChange> Changes,
    IReadOnlyList<SyncTombstone> Tombstones,
    IReadOnlyList<SyncUnclaimed> Unclaimed,
    IReadOnlyList<SyncCollision> Collisions);

/// <summary>
/// One aggregate crossing the wire, in either direction, as a versioned opaque document. The server
/// stores and returns <see cref="Body"/> without deserializing it: Platform knows the kind, the id
/// and the version, never the app's schema (ADR-0016, ADR-0033).
/// </summary>
/// <param name="Kind">The aggregate kind — <c>"group"</c> or <c>"profile"</c>.</param>
/// <param name="Id">The client-minted aggregate identity (ADR-0008).</param>
/// <param name="Version">The per-aggregate version, bumped by the client on each local edit.</param>
/// <param name="Body">
/// The aggregate's full body as opaque JSON. For a group this is the Secret-Santa group document;
/// Platform passes it through untouched.
/// </param>
public sealed record SyncChange(string Kind, Guid Id, long Version, JsonElement Body);

/// <summary>A deletion travelling as a tombstone: the aggregate is gone everywhere (FR-024).</summary>
/// <param name="Kind">The aggregate kind the deletion applies to.</param>
/// <param name="Id">The deleted aggregate's identity.</param>
/// <param name="Version">The version at deletion, so a later re-creation is not re-deleted.</param>
public sealed record SyncTombstone(string Kind, Guid Id, long Version);

/// <summary>
/// A group removed from the account on another device. Distinct from a tombstone: the local copy is
/// kept and becomes account-free again (FR-026).
/// </summary>
/// <param name="Id">The un-claimed group's identity.</param>
public sealed record SyncUnclaimed(Guid Id);

/// <summary>
/// A last-write-wins discard: the pushed body was older than the stored one by server time and was
/// dropped in full. Carries no content — enough to count and locate the loss, nothing that would put
/// a discarded body where it does not belong (ADR-0012, ADR-0020).
/// </summary>
/// <param name="Kind">The discarded aggregate's kind.</param>
/// <param name="Id">The discarded aggregate's identity.</param>
public sealed record SyncCollision(string Kind, Guid Id);
