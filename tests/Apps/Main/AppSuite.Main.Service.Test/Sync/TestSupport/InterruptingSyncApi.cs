using AppSuite.ApiClient;
using AppSuite.Main.Contracts;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// Wraps a real <see cref="ISyncApi"/> and drops the response of the first successful exchange
/// (FEAT-004 harness). The server still applies the push — the client just never learns it did, and
/// reports the cycle as unreachable — which is exactly the interruption that makes a client re-send an
/// already-applied push. It is the honest way to reproduce "already-applied push, replayed" without
/// reaching into client internals.
/// </summary>
/// <param name="inner">The real transport whose first response is swallowed.</param>
public sealed class InterruptingSyncApi(ISyncApi inner) : ISyncApi
{
    private bool _dropped;

    /// <inheritdoc />
    public async Task<ApiResult<SyncResponse>> ExchangeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.ExchangeAsync(request, cancellationToken);

        // Let the server apply the first push, then hide the outcome once: the client keeps the change
        // dirty and its cursor unmoved, so the next cycle re-sends the same version.
        if (!_dropped && result.IsSuccess)
        {
            _dropped = true;
            return ApiResult.Failed<SyncResponse>(new ApiError(ApiFailure.Unreachable));
        }

        return result;
    }

    /// <inheritdoc />
    public Task<ApiResult> ClaimAsync(Guid groupId, ClaimGroupRequest request, CancellationToken cancellationToken = default) =>
        inner.ClaimAsync(groupId, request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> UnclaimAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        inner.UnclaimAsync(groupId, cancellationToken);
}
