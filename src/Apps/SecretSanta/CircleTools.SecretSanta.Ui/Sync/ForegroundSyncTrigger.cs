using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The resume and foreground-poll sync triggers (ADR-0044 amendment 2026-10-15): coming back to the
/// foreground requests a sync, and while the app stays in the foreground a sync is requested every
/// <see cref="PollInterval"/>. Changes made on another device then arrive without a pull-to-refresh. A
/// background app never polls.
/// </summary>
/// <param name="autoSync">The background sync to request.</param>
/// <param name="foreground">Whether the app is in the foreground.</param>
public sealed class ForegroundSyncTrigger(AutoSyncService autoSync, IAppForeground foreground) : IDisposable
{
    /// <summary>How often a sync is requested while the app stays in the foreground.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    private CancellationTokenSource? _poll;
    private bool _initialized;

    /// <summary>Starts watching the foreground state and polling. Idempotent; never throws.</summary>
    /// <returns>A task that completes once watching has started.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        foreground.Changed += OnForegroundChanged;

        try
        {
            await foreground.InitializeAsync();
        }
        catch
        {
            // Best-effort: without the signal the app stays on its other triggers.
        }

        if (foreground.IsForeground)
        {
            StartPolling();
        }
    }

    private void OnForegroundChanged(bool isForeground)
    {
        if (isForeground)
        {
            autoSync.RequestSync(); // Resume: pull what changed while away.
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    private void StartPolling()
    {
        StopPolling();
        var cts = new CancellationTokenSource();
        _poll = cts;
        _ = PollAsync(cts.Token);
    }

    private void StopPolling()
    {
        _poll?.Cancel();
        _poll?.Dispose();
        _poll = null;
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                autoSync.RequestSync();
            }
        }
        catch (OperationCanceledException)
        {
            // Went to the background, or disposed.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreground.Changed -= OnForegroundChanged;
        StopPolling();
    }
}
