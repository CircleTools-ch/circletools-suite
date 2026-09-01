using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.SecretSanta.WebApp.Interop;

/// <summary>
/// Presents the share sheet through the Web Share API in <c>wwwroot/js/share.js</c> (ADR-0028).
/// </summary>
/// <remarks>
/// The module is imported on first use rather than at startup (mirrors <see cref="BrowserPasskeyCeremony"/>).
/// Callers warm it by asking <see cref="IsSupportedAsync"/> as the Share page renders, so the later
/// <see cref="ShareAsync"/> — which must run inside the click's transient user activation — is a direct
/// call with no import round-trip in front of it.
/// </remarks>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserShareService(IJSRuntime js) : IShareService, IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            return await module.InvokeAsync<bool>("canShare", cancellationToken);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask ShareAsync(
        string title,
        string text,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            await module.InvokeVoidAsync("share", cancellationToken, title, text, url);
        }
        catch (JSException)
        {
            // Best-effort (IShareService): a browser that refuses the share leaves the always-present
            // Copy button as the fallback. Nothing above this seam depends on the sheet appearing.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is going away; there is no runtime left to release the module in.
        }
    }

    private async ValueTask<IJSObjectReference> LoadAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/share.js");
}
