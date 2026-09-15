using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.WebApp.Interop;

/// <summary>
/// Web <see cref="INotificationScheduler"/> — a deliberate no-op (MNF-8, ADR-0060). A browser/PWA cannot
/// reliably fire a scheduled local notification while the tab is closed, so device-local reminders are a
/// phone-first feature (like haptics). Reporting "not supported" makes the reminder scheduler a clean
/// no-op here and lets the Settings screen explain that reminders need the app.
/// </summary>
public sealed class BrowserNotificationScheduler : INotificationScheduler
{
    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<bool> RequestPermissionAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask ScheduleAsync(ScheduledNotification notification, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask CancelAllAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
