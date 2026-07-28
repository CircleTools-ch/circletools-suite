using AppSuite.Ui.Abstractions;
using Microsoft.JSInterop;

namespace AppSuite.Main.WebApp.Storage;

/// <summary>
/// Maps <see cref="StorageScope"/> onto the browser's two web-storage areas.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StorageScope.Session"/> is <c>sessionStorage</c> (gone when the tab closes) and
/// <see cref="StorageScope.Durable"/> is <c>localStorage</c> (kept until removed). That is exactly
/// the distinction the "stay signed in" checkbox offers (ADR-0026).
/// </para>
/// <para>
/// Every call is wrapped: web storage throws in private-browsing modes and wherever site data is
/// blocked. A blocked store means "nothing was remembered", never a broken page — the app still
/// works, the user just signs in again.
/// </para>
/// </remarks>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserKeyValueStore(IJSRuntime js) : IKeyValueStore
{
    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(
        StorageScope scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await js.InvokeAsync<string?>($"{Area(scope)}.getItem", cancellationToken, key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync($"{Area(scope)}.setItem", cancellationToken, key, value);
        }
        catch (JSException)
        {
            // Storage unavailable or full. The value is lost, which costs a re-login at worst.
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(
        StorageScope scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync($"{Area(scope)}.removeItem", cancellationToken, key);
        }
        catch (JSException)
        {
            // Nothing could have been stored either, so there is nothing to remove.
        }
    }

    private static string Area(StorageScope scope) =>
        scope == StorageScope.Durable ? "localStorage" : "sessionStorage";
}
