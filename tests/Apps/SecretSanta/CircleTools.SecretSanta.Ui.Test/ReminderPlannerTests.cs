// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="ReminderPlanner"/> (MNF-8, FR-046): the per-kind timing rules, the gates
/// (draw done, wishlist filled, kind disabled), and that past reminders are never scheduled.
/// </summary>
public sealed class ReminderPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly ReminderLeadTimes Lead = new();

    private static HashSet<ReminderKind> Kinds(params ReminderKind[] kinds) => [.. kinds];

    private static OccasionReminderInput Occasion(bool hasDraw = false, bool wishlistEmpty = false, int day = 24) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Christmas 2026", new DateOnly(2026, 12, day), hasDraw, wishlistEmpty);

    [Fact]
    public void AllThreeKinds_ProduceThreeReminders_ForAFutureUndrawnEventWithAnEmptyWishlist()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(hasDraw: false, wishlistEmpty: true)],
            Kinds(ReminderKind.DrawNotDone, ReminderKind.EventTomorrow, ReminderKind.MyWishlistEmpty),
            Now, Lead);

        Assert.Equal(3, planned.Count);
        Assert.Contains(planned, p => p.Kind == ReminderKind.DrawNotDone);
        Assert.Contains(planned, p => p.Kind == ReminderKind.EventTomorrow);
        Assert.Contains(planned, p => p.Kind == ReminderKind.MyWishlistEmpty);
    }

    [Fact]
    public void DrawReminder_IsSkipped_WhenTheEventIsAlreadyDrawn()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(hasDraw: true)], Kinds(ReminderKind.DrawNotDone), Now, Lead);

        Assert.Empty(planned);
    }

    [Fact]
    public void WishlistReminder_IsSkipped_WhenTheWishlistIsNotEmpty()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(wishlistEmpty: false)], Kinds(ReminderKind.MyWishlistEmpty), Now, Lead);

        Assert.Empty(planned);
    }

    [Fact]
    public void OnlyEnabledKinds_AreScheduled()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(hasDraw: false, wishlistEmpty: true)], Kinds(ReminderKind.EventTomorrow), Now, Lead);

        Assert.Single(planned);
        Assert.Equal(ReminderKind.EventTomorrow, planned[0].Kind);
    }

    [Fact]
    public void NothingEnabled_SchedulesNothing()
    {
        var planned = ReminderPlanner.Plan([Occasion()], Kinds(), Now, Lead);

        Assert.Empty(planned);
    }

    [Fact]
    public void PastReminderTimes_AreNeverScheduled()
    {
        // The event is today, so every lead time lands before "now" — nothing is scheduled.
        var planned = ReminderPlanner.Plan(
            [Occasion(hasDraw: false, wishlistEmpty: true, day: 1)],
            Kinds(ReminderKind.DrawNotDone, ReminderKind.EventTomorrow, ReminderKind.MyWishlistEmpty),
            Now, Lead);

        Assert.Empty(planned);
    }

    [Fact]
    public void EventTomorrowReminder_FiresTheDayBefore_AtNineLocal()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(day: 24)], Kinds(ReminderKind.EventTomorrow), Now, Lead);

        var reminder = Assert.Single(planned);
        Assert.Equal(new DateTimeOffset(2026, 12, 23, 9, 0, 0, Now.Offset), reminder.When);
    }

    [Fact]
    public void Reminders_AreReturnedInChronologicalOrder()
    {
        var planned = ReminderPlanner.Plan(
            [Occasion(day: 31), Occasion(day: 24)],
            Kinds(ReminderKind.EventTomorrow),
            Now, Lead);

        Assert.Equal(2, planned.Count);
        Assert.True(planned[0].When < planned[1].When);
    }
}
