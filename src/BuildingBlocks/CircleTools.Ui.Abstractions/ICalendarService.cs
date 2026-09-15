namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Hands one occasion to the platform's own calendar UI, <strong>pre-filled</strong>, for the person to
/// review and save (MNF-6, FR-044). The app never writes to the calendar silently and never asks for a
/// calendar permission — the platform's add/preview screen is always shown, so the user sees exactly what
/// is being added.
/// </summary>
/// <remarks>
/// <para>
/// One of the platform seams (ADR-0028, its 2026-09-15 amendment): the shared iCalendar (<c>.ics</c>)
/// entry is built head-agnostically by <see cref="IcsBuilder"/>; only <em>delivery</em> is per-head. The
/// browser hands the <c>.ics</c> to the OS; the MAUI Android head fires an <c>ACTION_INSERT</c> intent
/// (Android has no reliable default handler for a <c>text/calendar</c> file); iOS opens the <c>.ics</c>
/// (deferred with the iOS head, ADR-0031).
/// </para>
/// <para>
/// It is a convenience, never a gate: everything above it is shared feature code that never knows which
/// head it runs on, and an entry is only offered when the occasion has a date (FR-044).
/// </para>
/// </remarks>
public interface ICalendarService
{
    /// <summary>Whether this platform can hand an occasion to a calendar at all.</summary>
    /// <remarks>
    /// True on the device heads and on browsers that can open/download the <c>.ics</c> (effectively
    /// everywhere). Callers use it to decide whether to show the "Add to calendar" action.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when <see cref="AddToCalendarAsync"/> can present the calendar UI.</returns>
    ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default);

    /// <summary>Presents the platform's pre-filled calendar UI for the given occasion.</summary>
    /// <remarks>
    /// Best-effort: dismissing the calendar UI without saving is a normal outcome and completes without
    /// error, and nothing is written unless the user confirms.
    /// </remarks>
    /// <param name="calendarEvent">The occasion to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the calendar UI has been presented.</returns>
    ValueTask AddToCalendarAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
}
