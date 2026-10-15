using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.Ui.Web;

/// <summary>
/// <see cref="IAppForeground"/> over the Page Visibility API (<c>document.visibilityState</c>), through
/// the <c>circletoolsForeground</c> bridge in the host's <c>js/app.js</c> (an external file: the CSP has
/// no 'unsafe-inline'). Mirrors <see cref="BrowserConnectivity"/>.
/// </summary>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserForeground(IJSRuntime js) : IAppForeground, IAsyncDisposable
{
    private DotNetObjectReference<BrowserForeground>? _self;

    /// <inheritdoc />
    public bool IsForeground { get; private set; } = true;

    /// <inheritdoc />
    public event Action<bool>? Changed;

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_self is not null)
        {
            return;
        }

        try
        {
            IsForeground = await js.InvokeAsync<bool>("circletoolsForeground.isVisible", cancellationToken);
            _self = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("circletoolsForeground.register", cancellationToken, _self);
        }
        catch (JSException)
        {
            // No bridge: stay "foreground". The other sync triggers still run.
        }
    }

    /// <summary>Called from JavaScript when the page's visibility changes.</summary>
    /// <param name="visible">Whether the page is now visible.</param>
    [JSInvokable]
    public void OnVisibilityChanged(bool visible)
    {
        if (visible == IsForeground)
        {
            return;
        }

        IsForeground = visible;
        Changed?.Invoke(visible);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        return ValueTask.CompletedTask;
    }
}
