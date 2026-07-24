using Microsoft.JSInterop;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// <see cref="IKeyValueStore"/> backed by the browser's <c>localStorage</c> via JS interop.
/// Requires no custom JavaScript file — it calls the built-in <c>localStorage</c> functions.
/// </summary>
/// <param name="jsRuntime">The Blazor JS runtime.</param>
public sealed class LocalStorageKeyValueStore(IJSRuntime jsRuntime) : IKeyValueStore
{
    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken) =>
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value);

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
}
