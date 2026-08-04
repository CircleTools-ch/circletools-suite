using AppSuite.Main.Contracts;

namespace AppSuite.Main.Service.Core.Domain;

/// <summary>
/// The server side of the M3 sync cycle (FEAT-004): applies a device's pushed changes to the
/// account-scoped store and returns everything that changed on the account above the device's cursor.
/// </summary>
/// <remarks>
/// This is Platform, not Secret-Santa. The store keeps each app aggregate as a versioned, opaque
/// document keyed by account and id — it knows the kind, the id, the version and the server-recorded
/// time, never the app's schema (ADR-0016, ADR-0033). One implementation covers every aggregate kind
/// (<c>group</c>, <c>profile</c>) because none of the logic depends on the body.
/// </remarks>
public interface ISyncStore
{
    /// <summary>Runs one delta exchange for a device, inside a single transaction.</summary>
    /// <param name="accountId">The calling account, taken from the bearer token — the only scope.</param>
    /// <param name="request">The device's cursor and its locally-dirty changes and deletions.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The delta above the cursor, the new cursor, and any last-write-wins collisions.</returns>
    Task<SyncResponse> ExchangeAsync(Guid accountId, SyncRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a group from the account (FR-026, <c>DELETE /api/v1/sync/groups/{id}</c>): deletes the
    /// server-side copy — its in-body assignments with it (ADR-0020) — and records an
    /// <em>un-claim marker</em> so the account's other devices drop the account scope but keep their
    /// local copy. An un-claim marker is deliberately not a tombstone: it must never delete a local copy.
    /// </summary>
    /// <remarks>
    /// Idempotent: un-claiming a group already removed elsewhere writes the marker (or refreshes it) and
    /// returns without error, so a caller can retry freely. A standing tombstone for the same id wins —
    /// a delete removes the copy everywhere and must not be downgraded to a keep-the-copy un-claim, which
    /// would let the deleted group survive on a device that had not yet seen the delete.
    /// </remarks>
    /// <param name="accountId">The calling account, taken from the bearer token — the only scope.</param>
    /// <param name="groupId">The group to remove from the account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the un-claim has been recorded.</returns>
    Task UnclaimAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default);
}
