// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The configurable reminders of FR-046's 2026-10-16 amendment (T4, Q9): lead time and time of day, per-occasion
/// overrides, the delivery time resolved with the target date's own UTC offset, and the settings round trip.
/// </summary>
public sealed class ReminderSettingsTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Zurich");

    private static readonly HashSet<ReminderKind> EventOnly = [ReminderKind.EventTomorrow];

    private static OccasionReminderInput Christmas(Guid? eventId = null) =>
        new(Guid.NewGuid(), eventId ?? Guid.NewGuid(), "Christmas", new DateOnly(2026, 12, 24), HasDraw: true, MyWishlistEmpty: false);

    [Fact]
    public void AReminderPlannedInSummer_ForAWinterDate_KeepsItsLocalTime()
    {
        // 16 October is summer time (+02:00); 23 December is winter time (+01:00). Q9: it used to fire at 08:00.
        var octoberNow = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(2));

        var planned = Assert.Single(ReminderPlanner.Plan([Christmas()], EventOnly, octoberNow, ReminderSettings.Default, Zurich));

        Assert.Equal(new DateTime(2026, 12, 23, 9, 0, 0), planned.When.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), planned.When.Offset);
    }

    [Fact]
    public void AReminderPlannedInWinter_ForASummerDate_KeepsItsLocalTime()
    {
        var februaryNow = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.FromHours(1));
        var midsummer = new OccasionReminderInput(Guid.NewGuid(), Guid.NewGuid(), "Midsummer", new DateOnly(2026, 6, 21), true, false);

        var planned = Assert.Single(ReminderPlanner.Plan([midsummer], EventOnly, februaryNow, ReminderSettings.Default, Zurich));

        Assert.Equal(new DateTime(2026, 6, 20, 9, 0, 0), planned.When.DateTime);
        Assert.Equal(TimeSpan.FromHours(2), planned.When.Offset);
    }

    [Fact]
    public void ATimeSkippedByTheSpringForward_MovesPastTheGap()
    {
        // 29 March 2026, 02:00–03:00 does not exist in Zurich.
        var settings = ReminderSettings.Default with { TimeOfDay = new TimeOnly(2, 30) };
        var easterish = new OccasionReminderInput(Guid.NewGuid(), Guid.NewGuid(), "Spring", new DateOnly(2026, 3, 30), true, false);

        var planned = Assert.Single(ReminderPlanner.Plan(
            [easterish], EventOnly, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(1)), settings, Zurich));

        Assert.Equal(new DateTime(2026, 3, 29, 3, 30, 0), planned.When.DateTime);
    }

    [Fact]
    public void LeadAndTimeOfDay_AreHonoured_AndTheLeadIsCarriedForTheText()
    {
        var settings = ReminderSettings.Default with
        {
            Lead = new ReminderLeadTimes(EventTomorrowDays: 5),
            TimeOfDay = new TimeOnly(7, 30),
        };

        var planned = Assert.Single(ReminderPlanner.Plan(
            [Christmas()], EventOnly, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(1)), settings, Zurich));

        Assert.Equal(new DateTime(2026, 12, 19, 7, 30, 0), planned.When.DateTime);
        Assert.Equal(5, planned.DaysBefore);
    }

    [Fact]
    public void AnOccasionsOverride_TurnsItOff_OrSetsItsOwnLead()
    {
        var off = Guid.NewGuid();
        var own = Guid.NewGuid();
        var settings = ReminderSettings.Default with
        {
            Overrides = new Dictionary<Guid, EventReminderOverride>
            {
                [off] = new(true, null),
                [own] = new(false, 7),
            },
        };

        var planned = ReminderPlanner.Plan(
            [Christmas(off), Christmas(own)],
            new HashSet<ReminderKind> { ReminderKind.EventTomorrow, ReminderKind.MyWishlistEmpty, ReminderKind.DrawNotDone },
            new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(1)),
            settings,
            Zurich);

        var only = Assert.Single(planned);
        Assert.Equal(own, only.EventId);
        Assert.Equal(7, only.DaysBefore);
    }

    [Fact]
    public async Task Settings_RoundTrip_AndOffersAreRememberedOnce()
    {
        var prefs = new NotificationPreferences(new InMemoryKeyValueStore());
        var eventId = Guid.NewGuid();
        var settings = new ReminderSettings(
            new ReminderLeadTimes(5, 2, 14),
            new TimeOnly(18, 30),
            new Dictionary<Guid, EventReminderOverride> { [eventId] = new(false, 3), [Guid.Empty] = new(true, null) });

        await prefs.SetSettingsAsync(settings);
        Assert.True(await prefs.MarkOfferedAsync(eventId));
        Assert.False(await prefs.MarkOfferedAsync(eventId));
        var read = await prefs.GetSettingsAsync();

        Assert.Equal(settings.Lead, read.Lead);
        Assert.Equal(settings.TimeOfDay, read.TimeOfDay);
        Assert.Equal(new EventReminderOverride(false, 3), read.Overrides[eventId]);
        Assert.Equal(new EventReminderOverride(true, null), read.Overrides[Guid.Empty]);
    }

    [Fact]
    public async Task UnsetSettings_AreTheDefaults()
    {
        var read = await new NotificationPreferences(new InMemoryKeyValueStore()).GetSettingsAsync();

        Assert.Equal(new ReminderLeadTimes(), read.Lead);
        Assert.Equal(new TimeOnly(9, 0), read.TimeOfDay);
        Assert.Empty(read.Overrides);
    }

    private sealed class InMemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _values = [];

        public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return ValueTask.FromResult(true);
        }

        public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
