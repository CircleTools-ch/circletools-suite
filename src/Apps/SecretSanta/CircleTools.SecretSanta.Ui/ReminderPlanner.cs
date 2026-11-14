// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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
/// <param name="DaysBefore">How many days before the occasion it fires, so the text can say "tomorrow" or "in N days".</param>
public sealed record PlannedReminder(ReminderKind Kind, Guid EventId, string EventName, DateTimeOffset When, int DaysBefore = 1);

/// <summary>How many days before the event each reminder fires (MNF-8 defaults, configurable since FR-046's 2026-10-16 amendment).</summary>
/// <param name="DrawNotDoneDays">Lead for the "draw not done yet" reminder (default 3).</param>
/// <param name="EventTomorrowDays">Lead for the "before the event" reminder (default 1).</param>
/// <param name="MyWishlistEmptyDays">Lead for the "my wishlist is empty" reminder (default 3).</param>
public sealed record ReminderLeadTimes(int DrawNotDoneDays = 3, int EventTomorrowDays = 1, int MyWishlistEmptyDays = 3)
{
    /// <summary>Gets the lead times a user can choose (days before the occasion).</summary>
    public static IReadOnlyList<int> Choices { get; } = [1, 2, 3, 5, 7, 14];

    /// <summary>The lead for one kind.</summary>
    /// <param name="kind">The reminder kind.</param>
    /// <returns>Days before the occasion.</returns>
    public int For(ReminderKind kind) => kind switch
    {
        ReminderKind.DrawNotDone => DrawNotDoneDays,
        ReminderKind.MyWishlistEmpty => MyWishlistEmptyDays,
        _ => EventTomorrowDays,
    };

    /// <summary>A copy with one kind's lead changed.</summary>
    /// <param name="kind">The reminder kind.</param>
    /// <param name="days">Days before the occasion.</param>
    /// <returns>The changed lead times.</returns>
    public ReminderLeadTimes With(ReminderKind kind, int days) => kind switch
    {
        ReminderKind.DrawNotDone => this with { DrawNotDoneDays = days },
        ReminderKind.MyWishlistEmpty => this with { MyWishlistEmptyDays = days },
        _ => this with { EventTomorrowDays = days },
    };
}

/// <summary>One occasion's own reminder setting (FR-046 amendment): off, or its own lead for the event reminder.</summary>
/// <param name="Off">No reminders at all for this occasion.</param>
/// <param name="EventLeadDays">This occasion's lead for the "before the event" reminder, or <see langword="null"/> for the default.</param>
public sealed record EventReminderOverride(bool Off, int? EventLeadDays);

/// <summary>The user's reminder settings on this device (FR-046 amendment 2026-10-16).</summary>
/// <param name="Lead">The lead per kind.</param>
/// <param name="TimeOfDay">When reminders are delivered, local time.</param>
/// <param name="Overrides">Per-occasion settings, by event id.</param>
public sealed record ReminderSettings(
    ReminderLeadTimes Lead,
    TimeOnly TimeOfDay,
    IReadOnlyDictionary<Guid, EventReminderOverride> Overrides)
{
    /// <summary>Gets the defaults: 3 / 1 / 3 days, at 09:00, no overrides.</summary>
    public static ReminderSettings Default { get; } =
        new(new ReminderLeadTimes(), new TimeOnly(9, 0), new Dictionary<Guid, EventReminderOverride>());
}

/// <summary>
/// Decides which device-local reminders to schedule from the local occasions and the user's chosen kinds
/// (MNF-8, FR-046). Pure and dependency-free so the timing rules are unit-tested directly; the
/// <c>ReminderScheduler</c> feeds it local data and turns its output into localized notifications.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>Plans with the given lead times, at 09:00, in the fixed offset of <paramref name="now"/>.</summary>
    /// <param name="occasions">The dated occasions from local data.</param>
    /// <param name="enabledKinds">The reminder kinds the user has switched on.</param>
    /// <param name="now">The current local time.</param>
    /// <param name="lead">The per-kind lead times.</param>
    /// <returns>The reminders to schedule.</returns>
    public static IReadOnlyList<PlannedReminder> Plan(
        IReadOnlyList<OccasionReminderInput> occasions,
        IReadOnlySet<ReminderKind> enabledKinds,
        DateTimeOffset now,
        ReminderLeadTimes lead) =>
        Plan(
            occasions,
            enabledKinds,
            now,
            ReminderSettings.Default with { Lead = lead },
            TimeZoneInfo.CreateCustomTimeZone("fixed", now.Offset, "fixed", "fixed"));

    /// <summary>Plans the reminders to schedule, in chronological order, skipping any already in the past.</summary>
    /// <param name="occasions">The dated occasions from local data.</param>
    /// <param name="enabledKinds">The reminder kinds the user has switched on.</param>
    /// <param name="now">The current time.</param>
    /// <param name="settings">Lead times, time of day and per-occasion overrides.</param>
    /// <param name="zone">
    /// The device's time zone. The delivery time is the chosen local time <em>on the target date</em>, with that
    /// date's UTC offset: a reminder planned in summer for a winter date must not shift by the DST hour (Q9).
    /// </param>
    /// <returns>The reminders to schedule.</returns>
    public static IReadOnlyList<PlannedReminder> Plan(
        IReadOnlyList<OccasionReminderInput> occasions,
        IReadOnlySet<ReminderKind> enabledKinds,
        DateTimeOffset now,
        ReminderSettings settings,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(occasions);
        ArgumentNullException.ThrowIfNull(enabledKinds);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(zone);

        var planned = new List<PlannedReminder>();
        if (enabledKinds.Count == 0)
        {
            return planned;
        }

        foreach (var occasion in occasions)
        {
            var own = settings.Overrides.GetValueOrDefault(occasion.EventId);
            if (own is { Off: true })
            {
                continue;
            }

            if (enabledKinds.Contains(ReminderKind.DrawNotDone) && !occasion.HasDraw)
            {
                Add(planned, ReminderKind.DrawNotDone, occasion, settings.Lead.DrawNotDoneDays, settings.TimeOfDay, now, zone);
            }

            if (enabledKinds.Contains(ReminderKind.EventTomorrow))
            {
                var lead = own?.EventLeadDays ?? settings.Lead.EventTomorrowDays;
                Add(planned, ReminderKind.EventTomorrow, occasion, lead, settings.TimeOfDay, now, zone);
            }

            if (enabledKinds.Contains(ReminderKind.MyWishlistEmpty) && occasion.MyWishlistEmpty)
            {
                Add(planned, ReminderKind.MyWishlistEmpty, occasion, settings.Lead.MyWishlistEmptyDays, settings.TimeOfDay, now, zone);
            }
        }

        return planned.OrderBy(p => p.When).ToList();
    }

    private static void Add(
        List<PlannedReminder> planned,
        ReminderKind kind,
        OccasionReminderInput occasion,
        int leadDays,
        TimeOnly timeOfDay,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var local = occasion.Date.AddDays(-leadDays).ToDateTime(timeOfDay);

        // A wall-clock time skipped by a spring-forward has no offset of its own: move it past the gap.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var when = new DateTimeOffset(local, zone.GetUtcOffset(local));
        if (when > now)
        {
            planned.Add(new PlannedReminder(kind, occasion.EventId, occasion.EventName, when, leadDays));
        }
    }
}
