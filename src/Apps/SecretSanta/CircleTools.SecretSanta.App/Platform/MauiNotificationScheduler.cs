using CircleTools.Ui.Abstractions;
#if ANDROID || IOS
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;
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
                NotifyTime = notification.DeliverAt,
                Android = new AndroidScheduleOptions
                {
                    // No exact-alarm permission (ADR-0060), so Android may fire this late; allow-while-idle keeps
                    // Doze from holding it back for hours.
                    ScheduleMode = AndroidScheduleMode.InexactAllowWhileIdle,

                    // The plugin drops an alarm that fires more than AllowedDelay (default 1 minute) late — a
                    // reminder 6 minutes late vanished without a trace (R17). Show it until its day is over.
                    AllowedDelay = notification.ShowUntil - notification.DeliverAt,
                },
            },
        });
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ScheduledNotification>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
        return [.. pending
            .Where(r => r.Schedule?.NotifyTime is not null)
            .Select(r => new ScheduledNotification(
                r.NotificationId,
                r.Title ?? string.Empty,
                r.Description ?? string.Empty,
                r.Schedule!.NotifyTime!.Value))];
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count > 0)
        {
            LocalNotificationCenter.Current.Cancel([.. ids]);
        }

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
    public ValueTask<IReadOnlyList<ScheduledNotification>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<ScheduledNotification>>([]);

    /// <inheritdoc />
    public ValueTask CancelAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
#endif
}
