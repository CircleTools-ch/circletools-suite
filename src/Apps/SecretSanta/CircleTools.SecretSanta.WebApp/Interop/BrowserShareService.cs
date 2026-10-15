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
    public async ValueTask<ShareOutcome> ShareAsync(
        string title,
        string text,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            var shared = await module.InvokeAsync<bool>("share", cancellationToken, title, text, url);
            return shared ? ShareOutcome.Shared : ShareOutcome.Dismissed;
        }
        catch (JSException)
        {
            // Best-effort (IShareService): a browser that refuses the share leaves the always-present
            // Copy button as the fallback. Nothing went out.
            return ShareOutcome.Dismissed;
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
