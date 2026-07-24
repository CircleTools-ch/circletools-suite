namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// Minimal string key-value persistence abstraction. Implemented over browser <c>localStorage</c>
/// (WebAssembly) or a JSON file on disk (MAUI). Keeps the repository host-agnostic.
/// </summary>
public interface IKeyValueStore
{
    /// <summary>Reads the value for a key, or <see langword="null"/> if absent.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the stored value, or <see langword="null"/>.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Writes the value for a key, replacing any existing value.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the value has been written.</returns>
    Task SetAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>Removes the value for a key, if present.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the value has been removed.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
