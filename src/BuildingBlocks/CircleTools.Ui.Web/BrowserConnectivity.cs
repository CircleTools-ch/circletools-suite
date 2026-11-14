// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.Ui.Web;

/// <summary>
/// Browser <see cref="IConnectivity"/> over <c>navigator.onLine</c> and the window
/// <c>online</c>/<c>offline</c> events (ADR-0044, ADR-0028).
/// </summary>
/// <remarks>
/// The listener registration and the initial read go through the small <c>circletoolsConnectivity</c>
/// bridge the host's <c>index.html</c> defines — the same "rely on an ambient browser global" shape
/// <see cref="BrowserKeyValueStore"/> uses for web storage, so this block ships no static assets. A
/// missing bridge or a blocked call leaves the app optimistically "online"; connectivity is advisory
/// (NFR-001), so a degraded read never breaks anything.
/// </remarks>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserConnectivity(IJSRuntime js) : IConnectivity, IAsyncDisposable
{
    private DotNetObjectReference<BrowserConnectivity>? _self;

    /// <inheritdoc />
    public bool IsOnline { get; private set; } = true;

    /// <inheritdoc />
    public event Action<bool>? Changed;

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsOnline = await js.InvokeAsync<bool>("circletoolsConnectivity.isOnline", cancellationToken);
            _self = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("circletoolsConnectivity.register", cancellationToken, _self);
        }
        catch (JSException)
        {
            // No bridge, or a blocked call: stay optimistically online. Sync still fires on its other
            // triggers (sign-in, app start, every change), so nothing is stranded.
        }
    }

    /// <summary>Invoked from JS on a window <c>online</c>/<c>offline</c> event.</summary>
    /// <param name="online">The new connectivity state.</param>
    [JSInvokable]
    public void OnConnectivityChanged(bool online)
    {
        if (online == IsOnline)
        {
            return;
        }

        IsOnline = online;
        Changed?.Invoke(online);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        return ValueTask.CompletedTask;
    }
}
