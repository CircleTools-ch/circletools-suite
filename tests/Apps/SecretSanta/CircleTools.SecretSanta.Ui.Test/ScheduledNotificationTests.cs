using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="ScheduledNotification.ShowUntil"/> (R17, 2026-10-21 device test): Android fires an
/// inexact alarm late, and a late reminder must still be shown on the day it was meant for — but never on a later
/// day.
/// </summary>
public sealed class ScheduledNotificationTests
{
    private static readonly TimeSpan Zurich = TimeSpan.FromHours(2);

    private static ScheduledNotification At(DateTimeOffset when) =>
        new(7, "Christmas is tomorrow", "Your gift exchange is tomorrow.", when);

    [Fact]
    public void ShowUntil_IsTheEndOfTheDeliveryDay()
    {
        var reminder = At(new DateTimeOffset(2026, 10, 21, 15, 30, 0, Zurich));

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 0, 0, 0, Zurich), reminder.ShowUntil);
    }

    [Fact]
    public void ShowUntil_CoversTheSixMinuteDelayThatDroppedTheReminder()
    {
        // The alarm for 15:30 fired around 15:36; the plugin's one-minute default threw it away.
        var reminder = At(new DateTimeOffset(2026, 10, 21, 15, 30, 0, Zurich));

        Assert.True(reminder.DeliverAt.AddMinutes(6) < reminder.ShowUntil);
    }

    [Fact]
    public void ShowUntil_UsesTheReminderOwnOffset_NotUtc()
    {
        // 00:30 local is still the previous day in UTC; the window must follow the local day.
        var reminder = At(new DateTimeOffset(2026, 10, 21, 0, 30, 0, Zurich));

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 0, 0, 0, Zurich), reminder.ShowUntil);
    }
}
