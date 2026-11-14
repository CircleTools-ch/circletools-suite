// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="ReminderScheduleDiff"/> (R12, 2026-10-20 device test): a reconcile must never
/// cancel a reminder that may already be on screen, and must leave an unchanged schedule alone.
/// </summary>
public sealed class ReminderScheduleDiffTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 31, 0, TimeSpan.Zero);

    private static ScheduledNotification At(int id, DateTimeOffset when, string title = "Christmas is tomorrow") =>
        new(id, title, "Your gift exchange is tomorrow.", when);

    [Fact]
    public void AnUnchangedSchedule_CancelsAndSchedulesNothing()
    {
        var reminder = At(7, Now.AddDays(3));

        var changes = ReminderScheduleDiff.Compute([reminder], [reminder], Now);

        Assert.Empty(changes.Cancel);
        Assert.Empty(changes.Schedule);
    }

    [Fact]
    public void AReminderAlreadyDue_IsNeverCancelled_EvenWhenNoLongerPlanned()
    {
        // Delivered a minute ago and possibly still in the shade; the next plan no longer contains it.
        var shown = At(7, Now.AddMinutes(-1));

        var changes = ReminderScheduleDiff.Compute([shown], [], Now);

        Assert.Empty(changes.Cancel);
    }

    [Fact]
    public void AFutureReminderNoLongerPlanned_IsCancelled()
    {
        var changes = ReminderScheduleDiff.Compute([At(7, Now.AddDays(3))], [], Now);

        Assert.Equal([7], changes.Cancel);
        Assert.Empty(changes.Schedule);
    }

    [Fact]
    public void AChangedReminder_IsCancelledAndScheduledAgain()
    {
        var before = At(7, Now.AddDays(3), "Christmas is tomorrow");
        var after = At(7, Now.AddDays(3), "Weihnachten ist morgen");

        var changes = ReminderScheduleDiff.Compute([before], [after], Now);

        Assert.Equal([7], changes.Cancel);
        Assert.Equal([after], changes.Schedule);
    }

    [Fact]
    public void ANewReminder_IsScheduled_WithoutCancellingAnything()
    {
        var existing = At(7, Now.AddDays(3));
        var added = At(8, Now.AddDays(5));

        var changes = ReminderScheduleDiff.Compute([existing], [existing, added], Now);

        Assert.Empty(changes.Cancel);
        Assert.Equal([added], changes.Schedule);
    }

    [Fact]
    public void ADeliveryTimeHandedBackWithLessPrecision_CountsAsUnchanged()
    {
        var planned = At(7, new DateTimeOffset(2026, 12, 23, 7, 59, 59, 999, TimeSpan.Zero));
        var fromOs = planned with { DeliverAt = new DateTimeOffset(2026, 12, 23, 7, 59, 59, TimeSpan.Zero) };

        var changes = ReminderScheduleDiff.Compute([fromOs], [planned], Now);

        Assert.Empty(changes.Cancel);
        Assert.Empty(changes.Schedule);
    }

    [Fact]
    public void TheId_IsStable_AndDiffersPerKindEventAndTime()
    {
        var eventId = Guid.NewGuid();
        var when = Now.AddDays(3);
        var reminder = new PlannedReminder(ReminderKind.EventTomorrow, eventId, "Christmas", when);

        Assert.Equal(ReminderScheduleDiff.IdFor(reminder), ReminderScheduleDiff.IdFor(reminder with { EventName = "Renamed" }));
        Assert.NotEqual(ReminderScheduleDiff.IdFor(reminder), ReminderScheduleDiff.IdFor(reminder with { Kind = ReminderKind.DrawNotDone }));
        Assert.NotEqual(ReminderScheduleDiff.IdFor(reminder), ReminderScheduleDiff.IdFor(reminder with { EventId = Guid.NewGuid() }));
        Assert.NotEqual(ReminderScheduleDiff.IdFor(reminder), ReminderScheduleDiff.IdFor(reminder with { When = when.AddDays(1) }));
        Assert.True(ReminderScheduleDiff.IdFor(reminder) > 0);
    }
}
