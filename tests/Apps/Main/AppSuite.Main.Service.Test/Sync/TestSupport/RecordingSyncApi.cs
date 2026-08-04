using AppSuite.ApiClient;
using AppSuite.Main.Contracts;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// Forwards to a real <see cref="ISyncApi"/> and keeps the last request it sent and the last response
/// it received (FEAT-004 harness). Lets a test assert the shape of the wire itself — in particular
/// that a no-op cycle carries no aggregate bodies in <em>either</em> direction (NFR-008), which the
/// client's own outcome counts cannot prove because a body pulled-then-ignored would not show there.
/// </summary>
/// <param name="inner">The real transport to forward to.</param>
public sealed class RecordingSyncApi(ISyncApi inner) : ISyncApi
{
    /// <summary>The last request sent through this decorator, or null before the first cycle.</summary>
    public SyncRequest? LastRequest { get; private set; }

    /// <summary>The last successful response received, or null if none has completed.</summary>
    public SyncResponse? LastResponse { get; private set; }

    /// <inheritdoc />
    public async Task<ApiResult<SyncResponse>> ExchangeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        var result = await inner.ExchangeAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            LastResponse = result.Value;
        }

        return result;
    }

    /// <inheritdoc />
    public Task<ApiResult> UnclaimAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        inner.UnclaimAsync(groupId, cancellationToken);
}
