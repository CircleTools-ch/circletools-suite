// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Works out how to move the OS schedule from what is pending to what is planned, touching as little as
/// possible (R12, 2026-10-20 device test). Cancelling a notification on Android also removes it from the shade,
/// so a reconcile that cancelled everything wiped a reminder the user had just received. Here only reminders
/// that are still due in the future and no longer planned (or planned differently) are cancelled; an unchanged
/// schedule is left alone. Pure and deterministic.
/// </summary>
public static class ReminderScheduleDiff
{
    /// <summary>A stable notification id for a planned reminder: the same kind, event and time give the same id.</summary>
    /// <param name="reminder">The planned reminder.</param>
    /// <returns>A positive id.</returns>
    public static int IdFor(PlannedReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        var key = $"{reminder.Kind}|{reminder.EventId:N}|{reminder.When.UtcTicks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return (BitConverter.ToInt32(hash, 0) & int.MaxValue) | 1; // positive, never 0
    }

    /// <summary>Compares the pending notifications with the planned ones.</summary>
    /// <param name="pending">What the OS still holds for this app.</param>
    /// <param name="planned">What should be scheduled now.</param>
    /// <param name="now">The current time; anything at or before it may already be on screen and is never cancelled.</param>
    /// <returns>The ids to cancel and the notifications to schedule.</returns>
    public static ReminderScheduleChanges Compute(
        IReadOnlyList<ScheduledNotification> pending,
        IReadOnlyList<ScheduledNotification> planned,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(planned);

        var plannedById = planned.ToDictionary(p => p.Id);
        var futurePending = pending.Where(p => p.DeliverAt > now).ToList();

        var cancel = futurePending
            .Where(p => !plannedById.TryGetValue(p.Id, out var wanted) || !Same(p, wanted))
            .Select(p => p.Id)
            .Distinct()
            .ToList();

        var schedule = planned
            .Where(p => !futurePending.Any(q => q.Id == p.Id && Same(q, p)))
            .ToList();

        return new ReminderScheduleChanges(cancel, schedule);
    }

    // Times are compared to the minute: the OS may hand back the delivery time with less precision than sent.
    private static bool Same(ScheduledNotification a, ScheduledNotification b) =>
        a.Title == b.Title
        && a.Body == b.Body
        && Math.Abs((a.DeliverAt - b.DeliverAt).TotalMinutes) < 1;
}

/// <summary>The outcome of <see cref="ReminderScheduleDiff.Compute"/>.</summary>
/// <param name="Cancel">Ids of future notifications that are no longer wanted as scheduled.</param>
/// <param name="Schedule">Notifications to schedule (new, or replacing a cancelled one).</param>
public sealed record ReminderScheduleChanges(IReadOnlyList<int> Cancel, IReadOnlyList<ScheduledNotification> Schedule);
