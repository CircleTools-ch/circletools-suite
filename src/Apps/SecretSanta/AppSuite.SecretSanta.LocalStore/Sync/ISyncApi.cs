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
}
