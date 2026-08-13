using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Test.TestSupport;

/// <summary>
/// A scripted <see cref="ISyncApi"/> for driving <see cref="SyncService"/> cycles with no transport.
/// Records each request it receives and returns the next scripted response, or a benign empty
/// response that echoes the request's cursor when nothing is scripted.
/// </summary>
public sealed class FakeSyncApi : ISyncApi
{
    private readonly Queue<ApiResult<SyncResponse>> _responses = new();
    private readonly Queue<ApiResult> _unclaimResponses = new();
    private readonly Queue<ApiResult> _claimResponses = new();

    /// <summary>Every request the service sent, in order.</summary>
    public List<SyncRequest> Requests { get; } = [];

    /// <summary>Every group id passed to <see cref="ClaimAsync"/>, in order.</summary>
    public List<Guid> Claimed { get; } = [];

    /// <summary>Every group id passed to <see cref="UnclaimAsync"/>, in order.</summary>
    public List<Guid> Unclaimed { get; } = [];

    /// <summary>The most recent request the service sent.</summary>
    public SyncRequest? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    /// <summary>
    /// Invoked while a request is "in flight", before the response is returned — the seam for
    /// simulating a local edit made during the network round-trip.
    /// </summary>
    public Func<Task>? DuringExchange { get; set; }

    /// <summary>Scripts the next response to succeed with <paramref name="response"/>.</summary>
    /// <param name="response">The response to return.</param>
    /// <returns>This, for chaining.</returns>
    public FakeSyncApi Returns(SyncResponse response)
    {
        _responses.Enqueue(ApiResult.Ok(response));
        return this;
    }

    /// <summary>Scripts the next response to fail with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure category.</param>
    /// <returns>This, for chaining.</returns>
    public FakeSyncApi Fails(ApiFailure failure)
    {
        _responses.Enqueue(ApiResult.Failed<SyncResponse>(new ApiError(failure)));
        return this;
    }

    /// <inheritdoc />
    public async Task<ApiResult<SyncResponse>> ExchangeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        if (DuringExchange is not null)
        {
            await DuringExchange();
        }

        return _responses.Count > 0
            ? _responses.Dequeue()
            : ApiResult.Ok(Empty(request.Cursor));
    }

    /// <summary>Scripts the next <see cref="ClaimAsync"/> call to succeed. Order matters across calls.</summary>
    /// <returns>This, for chaining.</returns>
    public FakeSyncApi ClaimSucceeds()
    {
        _claimResponses.Enqueue(ApiResult.Ok());
        return this;
    }

    /// <summary>Scripts the next <see cref="ClaimAsync"/> call to fail with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure category.</param>
    /// <returns>This, for chaining.</returns>
    public FakeSyncApi ClaimFails(ApiFailure failure)
    {
        _claimResponses.Enqueue(ApiResult.Failed(new ApiError(failure)));
        return this;
    }

    /// <inheritdoc />
    public Task<ApiResult> ClaimAsync(
        Guid groupId,
        ClaimGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        Claimed.Add(groupId);
        return Task.FromResult(_claimResponses.Count > 0 ? _claimResponses.Dequeue() : ApiResult.Ok());
    }

    /// <summary>Scripts the next <see cref="UnclaimAsync"/> call to fail with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure category.</param>
    /// <returns>This, for chaining.</returns>
    public FakeSyncApi UnclaimFails(ApiFailure failure)
    {
        _unclaimResponses.Enqueue(ApiResult.Failed(new ApiError(failure)));
        return this;
    }

    /// <inheritdoc />
    public Task<ApiResult> UnclaimAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        Unclaimed.Add(groupId);
        return Task.FromResult(_unclaimResponses.Count > 0 ? _unclaimResponses.Dequeue() : ApiResult.Ok());
    }

    /// <summary>A response that accepts a push and returns nothing new, holding the cursor.</summary>
    /// <param name="cursor">The cursor to echo.</param>
    /// <returns>An empty successful response.</returns>
    public static SyncResponse Empty(long cursor) => new(cursor, [], [], [], []);
}
