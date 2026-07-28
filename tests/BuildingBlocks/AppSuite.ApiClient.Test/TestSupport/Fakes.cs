using System.Net;
using AppSuite.Ui.Abstractions;

namespace AppSuite.ApiClient.Test.TestSupport;

/// <summary>
/// The innermost handler in a test chain: an API that accepts exactly one token.
/// </summary>
/// <remarks>
/// Answering on the token rather than on a call counter is what makes the concurrency test
/// deterministic — every request holding the stale token is rejected, however the calls interleave.
/// </remarks>
/// <param name="validToken">The bearer token that is accepted; <see langword="null"/> accepts
/// unauthenticated requests.</param>
public sealed class TokenAwareHandler(string? validToken) : HttpMessageHandler
{
    private readonly Lock _gate = new();

    /// <summary>Every request that reached the wire, in arrival order.</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = request.Headers.Authorization?.Parameter;

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (_gate)
        {
            Requests.Add(new RecordedRequest(
                token,
                body,
                request.Options.TryGetValue(ApiRequestOptions.IncludeCredentials, out var include) && include));
        }

        return new HttpResponseMessage(
            string.Equals(token, validToken, StringComparison.Ordinal)
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized);
    }
}

/// <summary>What a single outgoing request looked like by the time it reached the wire.</summary>
/// <param name="BearerToken">The token on the Authorization header, if any.</param>
/// <param name="Body">The request body as text, if any.</param>
/// <param name="IncludeCredentials">Whether the cross-origin credentials marker survived.</param>
public sealed record RecordedRequest(string? BearerToken, string? Body, bool IncludeCredentials);

/// <summary>An <see cref="ISessionRefresher"/> that counts its calls and hands out a fixed answer.</summary>
/// <param name="tokens">The in-memory token store to update on a successful refresh.</param>
/// <param name="result">The new access token, or <see langword="null"/> to end the session.</param>
public sealed class CountingRefresher(IAccessTokenStore tokens, string? result = null) : ISessionRefresher
{
    private int _calls;

    /// <summary>How many times a refresh was actually attempted.</summary>
    public int Calls => _calls;

    /// <inheritdoc />
    public ValueTask<string?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);

        if (result is null)
        {
            tokens.Clear();
        }
        else
        {
            tokens.SetToken(result, TimeSpan.FromMinutes(15));
        }

        return ValueTask.FromResult(result);
    }
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> with no browser behind it.</summary>
public sealed class FakeKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<(StorageScope Scope, string Key), string> _values = [];

    /// <summary>Reads a value directly, bypassing the interface, for arrange and assert steps.</summary>
    /// <param name="scope">The scope to look in.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The stored value, or <see langword="null"/>.</returns>
    public string? Peek(StorageScope scope, string key) =>
        _values.TryGetValue((scope, key), out var value) ? value : null;

    /// <summary>
    /// When set, every write is refused — the store stays empty and <c>SetAsync</c> returns
    /// <see langword="false"/>, as a browser with storage blocked or full does.
    /// </summary>
    public bool RefuseWrites { get; set; }

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Peek(scope, key));

    /// <inheritdoc />
    public ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (RefuseWrites)
        {
            return ValueTask.FromResult(false);
        }

        _values[(scope, key)] = value;
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
    {
        _values.Remove((scope, key));
        return ValueTask.CompletedTask;
    }
}
