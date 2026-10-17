using CircleTools.Ui.Abstractions;
#if ANDROID || IOS
using Plugin.LocalNotification;
#endif

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="INotificationScheduler"/> over Plugin.LocalNotification (MNF-8, ADR-0060) — schedules
/// device-local reminders on the mobile heads. The Windows head has no scheduled-notification path wired
/// (reminders are a phone feature), so it reports unsupported and the reminder scheduler no-ops there.
/// </summary>
public sealed class MauiNotificationScheduler : INotificationScheduler
{
#if ANDROID || IOS
    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);

    /// <inheritdoc />
    public async ValueTask<bool> RequestPermissionAsync(CancellationToken cancellationToken = default) =>
        await LocalNotificationCenter.Current.RequestNotificationPermission();

    /// <inheritdoc />
    public async ValueTask<NotificationAccess> GetPermissionAsync(CancellationToken cancellationToken = default) =>
        await LocalNotificationCenter.Current.AreNotificationsEnabled()
            ? NotificationAccess.Granted
            : NotificationAccess.Denied;

    /// <inheritdoc />
    public ValueTask OpenSettingsAsync(CancellationToken cancellationToken = default)
    {
        // The app's own page in the system settings (Android: App info; iOS: the app's Settings page), where
        // notifications are switched back on after a "don't ask again".
        MainThread.BeginInvokeOnMainThread(AppInfo.Current.ShowSettingsUI);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync(ScheduledNotification notification, CancellationToken cancellationToken = default)
    {
        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = notification.Id,
            Title = notification.Title,
            Description = notification.Body,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notification.DeliverAt.LocalDateTime,
            },
        });
    }

    /// <inheritdoc />
    public ValueTask CancelAllAsync(CancellationToken cancellationToken = default)
    {
        LocalNotificationCenter.Current.CancelAll();
        return ValueTask.CompletedTask;
    }
#else
    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<bool> RequestPermissionAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<NotificationAccess> GetPermissionAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(NotificationAccess.Unknown);

    /// <inheritdoc />
    public ValueTask OpenSettingsAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ScheduleAsync(ScheduledNotification notification, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask CancelAllAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
#endif
}
