using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.Main.WebApp.Interop;

/// <summary>Downloads generated content through <c>wwwroot/js/download.js</c> (FR-014).</summary>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserFileSaver(IJSRuntime js) : IFileSaver, IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        string fileName,
        string contentType,
        string content,
        CancellationToken cancellationToken = default)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/download.js");
        await _module.InvokeVoidAsync("save", cancellationToken, fileName, contentType, content);
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
}
