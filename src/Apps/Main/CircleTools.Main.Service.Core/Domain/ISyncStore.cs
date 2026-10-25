using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// The server side of the M3 sync cycle (FEAT-004): applies a device's pushed changes to the
/// account-scoped store and returns everything that changed on the account above the device's cursor.
/// </summary>
/// <remarks>
/// This is Platform, not Secret-Santa. The store keeps each app aggregate as a versioned, opaque
/// document keyed by account and id — it knows the kind, the id, the version and the server-recorded
/// time, never the app's schema (ADR-0016, ADR-0033). One implementation covers every aggregate kind
/// (<c>profile</c>, <c>preferences</c>) because none of the logic depends on the body. Groups are not synced here:
/// online groups live in the Secret-Santa group store (ADR-0086).
/// </remarks>
public interface ISyncStore
{
    /// <summary>Runs one delta exchange for a device, inside a single transaction.</summary>
    /// <param name="accountId">The calling account, taken from the bearer token — the only scope.</param>
    /// <param name="request">The device's cursor and its locally-dirty changes and deletions.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The delta above the cursor, the new cursor, and any last-write-wins collisions.</returns>
    Task<SyncResponse> ExchangeAsync(Guid accountId, SyncRequest request, CancellationToken cancellationToken = default);
}

