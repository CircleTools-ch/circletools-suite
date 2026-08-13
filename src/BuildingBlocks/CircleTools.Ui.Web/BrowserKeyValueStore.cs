using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.Ui.Web;

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
/// Every call is wrapped: web storage throws in private-browsing modes, wherever site data is
/// blocked, and when the origin's quota is exhausted. A blocked store means "nothing was
/// remembered", never a broken page. A refused <em>write</em> is reported back rather than
/// swallowed, because whether that matters depends on what was being written — see
/// <see cref="IKeyValueStore"/>.
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
    public async ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync($"{Area(scope)}.setItem", cancellationToken, key, value);
            return true;
        }
        catch (JSException)
        {
            return false;
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
