namespace CircleTools.Ui.Abstractions;

/// <summary>
/// The kinds of device-local reminder a user can switch on (MNF-8, FR-046). Each is opt-in and off by
/// default; none is ever a cross-member push (that is parked, ADR-0060).
/// </summary>
public enum ReminderKind
{
    /// <summary>The organizer has not run the draw yet and the event is approaching.</summary>
    DrawNotDone,

    /// <summary>The event is tomorrow.</summary>
    EventTomorrow,

    /// <summary>The user's own wishlist for an upcoming event is still empty.</summary>
    MyWishlistEmpty,
}

/// <summary>One local notification to deliver at a future time.</summary>
/// <param name="Id">A per-reconcile id, unique within the scheduled set.</param>
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

    /// <summary>Schedules one notification for future delivery.</summary>
    /// <param name="notification">The notification to schedule.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once it is scheduled.</returns>
    ValueTask ScheduleAsync(ScheduledNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Cancels every pending notification this app scheduled (the reconcile clears then reschedules).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once all pending notifications are cancelled.</returns>
    ValueTask CancelAllAsync(CancellationToken cancellationToken = default);
}
