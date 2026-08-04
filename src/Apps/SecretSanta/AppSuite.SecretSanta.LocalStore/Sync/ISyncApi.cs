using AppSuite.ApiClient;
using AppSuite.Main.Contracts;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// The client's port onto the Platform delta-exchange endpoint (<c>POST /api/v1/sync</c>, FEAT-004).
/// </summary>
/// <remarks>
/// A port rather than a bare <see cref="System.Net.Http.HttpClient"/> so the <see cref="SyncService"/>
/// cycle can be driven in a unit test with no transport, and so an unreachable server is an
/// <see cref="ApiResult"/> value the cycle inspects — never an exception that aborts it and leaves the
/// local dirty flags in an unknown state.
/// </remarks>
public interface ISyncApi
{
    /// <summary>Runs one delta exchange: push the request's changes, receive everything above its cursor.</summary>
    /// <param name="request">The device id, cursor, dirty bodies and tombstones.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The server delta and new cursor, or the failure (an unreachable server included).</returns>
    Task<ApiResult<SyncResponse>> ExchangeAsync(SyncRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a group from the account (<c>DELETE /api/v1/sync/groups/{id}</c>, FR-026): deletes the
    /// server copy and records an un-claim marker for the account's other devices. Requires a connection,
    /// so an unreachable server is a failure the caller inspects — the local scope is only cleared once
    /// the server has confirmed, never on an unreachable server.
    /// </summary>
    /// <param name="groupId">The group to remove from the account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success once the server has recorded the removal, or the failure (unreachable included).</returns>
    Task<ApiResult> UnclaimAsync(Guid groupId, CancellationToken cancellationToken = default);
}
