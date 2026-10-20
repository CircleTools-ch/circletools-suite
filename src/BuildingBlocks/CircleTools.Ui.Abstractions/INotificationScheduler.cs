namespace CircleTools.Ui.Abstractions;

/// <summary>
/// The kinds of device-local reminder a user can switch on (MNF-8, FR-046). Each is opt-in and off by
/// default; none is ever a cross-member push (that is parked, ADR-0060).
/// </summary>
public enum ReminderKind
{
    /// <summary>The organizer has not run the draw yet and the event is approaching.</summary>
    DrawNotDone,

    /// <summary>The event is near — "before the event", with a configurable lead (the name predates that).</summary>
    EventTomorrow,

    /// <summary>The user's own wishlist for an upcoming event is still empty.</summary>
    MyWishlistEmpty,
}

/// <summary>Whether the OS lets this app show notifications (FR-046 amendment, Q8).</summary>
public enum NotificationAccess
{
    /// <summary>Notifications are allowed.</summary>
    Granted,

    /// <summary>Notifications are blocked for the app (refused, or turned off in the system settings).</summary>
    Denied,

    /// <summary>The platform can't tell (or has no notifications at all).</summary>
    Unknown,
}

/// <summary>One local notification to deliver at a future time.</summary>
/// <param name="Id">
/// A stable id for the reminder it stands for, so a reconcile can tell an unchanged reminder from a new one.
/// </param>
/// <param name="Title">The notification title (localized; never contains an assignment — NFR-002).</param>
/// <param name="Body">The notification body (localized; never contains an assignment).</param>
/// <param name="DeliverAt">When to deliver it (local time). Past times are never scheduled.</param>
public sealed record ScheduledNotification(int Id, string Title, string Body, DateTimeOffset DeliverAt);

/// <summary>
/// Schedules device-local notifications (MNF-8, ADR-0060). One of the platform seams (ADR-0028): the MAUI
/// heads schedule real OS notifications; the web head is a no-op (a browser/PWA cannot reliably fire a
/// scheduled notification while closed), so this is phone-first, like <c>IHapticFeedback</c>.
/// </summary>
/// <remarks>
/// The app never delivers a notification silently in the sense of surprising the user: reminders are only
/// scheduled for kinds the user switched on, permission is requested the first time they do, and no body
/// ever carries who-drew-whom (NFR-002).
/// </remarks>
public interface INotificationScheduler
{
    /// <summary>Whether this platform can schedule local notifications at all (false on the web).</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> on the device heads; <see langword="false"/> on the browser.</returns>
    ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests the OS notification permission, showing the platform prompt if needed.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when notifications are permitted after the request.</returns>
    ValueTask<bool> RequestPermissionAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads whether notifications are allowed, without prompting (Q8).</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The current permission.</returns>
    ValueTask<NotificationAccess> GetPermissionAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the app's page in the system settings, where notifications can be allowed (Q8).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the settings were asked to open.</returns>
    ValueTask OpenSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules one notification for future delivery.</summary>
    /// <param name="notification">The notification to schedule.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once it is scheduled.</returns>
    ValueTask ScheduleAsync(ScheduledNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Lists the notifications this app scheduled that the OS still holds for delivery.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The scheduled notifications, as far as the platform reports them.</returns>
    ValueTask<IReadOnlyList<ScheduledNotification>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels scheduled notifications by id. Only ever called for notifications still due in the future: on
    /// Android cancelling an id also removes it from the shade, so a reminder already shown must never be
    /// passed here (R12, 2026-10-20 device test).
    /// </summary>
    /// <param name="ids">The ids to cancel.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once they are cancelled.</returns>
    ValueTask CancelAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
