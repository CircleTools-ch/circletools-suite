using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>One occasion's inputs for reminder planning (MNF-8, FR-046), read from the local group data.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="EventId">The occasion.</param>
/// <param name="EventName">The event name (already defaulted for an unnamed event).</param>
/// <param name="Date">The occasion date.</param>
/// <param name="HasDraw">Whether the occasion has a current draw.</param>
/// <param name="MyWishlistEmpty">Whether the current user is a member here with an empty wishlist for this occasion.</param>
public sealed record OccasionReminderInput(
    Guid GroupId,
    Guid EventId,
    string EventName,
    DateOnly Date,
    bool HasDraw,
    bool MyWishlistEmpty);

/// <summary>A reminder the planner decided to schedule.</summary>
/// <param name="Kind">Which reminder.</param>
/// <param name="EventId">The occasion it is about.</param>
/// <param name="EventName">The event name, for the notification text.</param>
/// <param name="When">When to deliver it (local).</param>
public sealed record PlannedReminder(ReminderKind Kind, Guid EventId, string EventName, DateTimeOffset When);

/// <summary>How many days before the event each reminder fires (MNF-8 defaults).</summary>
/// <param name="DrawNotDoneDays">Lead for the "draw not done yet" reminder (default 3).</param>
/// <param name="EventTomorrowDays">Lead for the "event is tomorrow" reminder (default 1).</param>
/// <param name="MyWishlistEmptyDays">Lead for the "my wishlist is empty" reminder (default 3).</param>
public sealed record ReminderLeadTimes(int DrawNotDoneDays = 3, int EventTomorrowDays = 1, int MyWishlistEmptyDays = 3);

/// <summary>
/// Decides which device-local reminders to schedule from the local occasions and the user's chosen kinds
/// (MNF-8, FR-046). Pure and dependency-free so the timing rules are unit-tested directly; the
/// <c>ReminderScheduler</c> feeds it local data and turns its output into localized notifications.
/// </summary>
public static class ReminderPlanner
{
    private static readonly TimeOnly DeliverAtTime = new(9, 0); // a civil-hour reminder, not midnight

    /// <summary>Plans the reminders to schedule, in chronological order, skipping any already in the past.</summary>
    /// <param name="occasions">The dated occasions from local data.</param>
    /// <param name="enabledKinds">The reminder kinds the user has switched on.</param>
    /// <param name="now">The current local time.</param>
    /// <param name="lead">The per-kind lead times.</param>
    /// <returns>The reminders to schedule.</returns>
    public static IReadOnlyList<PlannedReminder> Plan(
        IReadOnlyList<OccasionReminderInput> occasions,
        IReadOnlySet<ReminderKind> enabledKinds,
        DateTimeOffset now,
        ReminderLeadTimes lead)
    {
        var planned = new List<PlannedReminder>();
        if (enabledKinds.Count == 0)
        {
            return planned;
        }

        foreach (var occasion in occasions)
        {
            if (enabledKinds.Contains(ReminderKind.DrawNotDone) && !occasion.HasDraw)
            {
                Add(planned, ReminderKind.DrawNotDone, occasion, lead.DrawNotDoneDays, now);
            }

            if (enabledKinds.Contains(ReminderKind.EventTomorrow))
            {
                Add(planned, ReminderKind.EventTomorrow, occasion, lead.EventTomorrowDays, now);
            }

            if (enabledKinds.Contains(ReminderKind.MyWishlistEmpty) && occasion.MyWishlistEmpty)
            {
                Add(planned, ReminderKind.MyWishlistEmpty, occasion, lead.MyWishlistEmptyDays, now);
            }
        }

        return planned.OrderBy(p => p.When).ToList();
    }

    private static void Add(
        List<PlannedReminder> planned, ReminderKind kind, OccasionReminderInput occasion, int leadDays, DateTimeOffset now)
    {
        var when = new DateTimeOffset(occasion.Date.AddDays(-leadDays).ToDateTime(DeliverAtTime), now.Offset);
        if (when > now)
        {
            planned.Add(new PlannedReminder(kind, occasion.EventId, occasion.EventName, when));
        }
    }
}
