using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.SecretSanta.LocalStore.Test.TestSupport;

/// <summary>
/// Hand-written in-memory <see cref="IKeyValueStore"/> used to exercise <c>GroupRepository</c>
/// without touching the browser or the file system.
/// </summary>
public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = [];

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    /// <inheritdoc />
    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
