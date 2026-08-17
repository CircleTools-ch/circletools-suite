using CircleTools.Ui.Abstractions;
using MauiNet = Microsoft.Maui.Networking;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="CircleTools.Ui.Abstractions.IConnectivity"/> over <c>Microsoft.Maui.Networking.Connectivity</c> (ADR-0044,
/// ADR-0028) — the device-head counterpart of <c>BrowserConnectivity</c>.
/// </summary>
/// <remarks>
/// Wraps the platform <see cref="MauiNet.Connectivity"/> singleton. "Online" means the device reports
/// <see cref="MauiNet.NetworkAccess.Internet"/>; any lesser access (local-only, none) reads as offline,
/// which is the conservative reading for whether a sync could reach the Main API. Connectivity is
/// advisory (NFR-001), so a wrong reading only defers or retries a sync — it never blocks editing.
/// </remarks>
public sealed class MauiConnectivity : CircleTools.Ui.Abstractions.IConnectivity, IDisposable
{
    private readonly MauiNet.IConnectivity _connectivity = MauiNet.Connectivity.Current;

    /// <inheritdoc />
    public bool IsOnline { get; private set; } = true;

    /// <inheritdoc />
    public event Action<bool>? Changed;

    /// <inheritdoc />
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsOnline = _connectivity.NetworkAccess == MauiNet.NetworkAccess.Internet;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        return ValueTask.CompletedTask;
    }

    private void OnConnectivityChanged(object? sender, MauiNet.ConnectivityChangedEventArgs e)
    {
        var online = e.NetworkAccess == MauiNet.NetworkAccess.Internet;
        if (online == IsOnline)
        {
            return;
        }

        IsOnline = online;
        Changed?.Invoke(online);
    }

    /// <inheritdoc />
    public void Dispose() => _connectivity.ConnectivityChanged -= OnConnectivityChanged;
}
