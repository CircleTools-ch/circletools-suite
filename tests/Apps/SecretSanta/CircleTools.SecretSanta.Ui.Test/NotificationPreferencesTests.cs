using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="NotificationPreferences"/> (MNF-8, FR-046): every reminder kind is off by
/// default (opt-in), toggles round-trip, and choices persist across app restarts on the same device.
/// </summary>
public sealed class NotificationPreferencesTests
{
    [Fact]
    public async Task EverythingIsOff_ByDefault()
    {
        var prefs = new NotificationPreferences(new InMemoryKeyValueStore());

        Assert.Empty(await prefs.GetEnabledAsync());
        Assert.False(await prefs.IsEnabledAsync(ReminderKind.DrawNotDone));
    }

    [Fact]
    public async Task AWishMadeWhileBlocked_IsSwitchedOnOnlyWhenApplied_AndThenForgotten()
    {
        // R15 (2026-10-20 device test): "Remind me" after a draw with notifications blocked must survive the trip
        // to the system settings and apply once they are allowed — and only the kind asked for.
        var store = new InMemoryKeyValueStore();
        await new NotificationPreferences(store).AddWantedAsync(ReminderKind.EventTomorrow);

        var prefs = new NotificationPreferences(store); // a later page or app start
        Assert.Empty(await prefs.GetEnabledAsync());
        Assert.Equal([ReminderKind.EventTomorrow], await prefs.GetWantedAsync());

        Assert.Equal([ReminderKind.EventTomorrow], await prefs.ApplyWantedAsync());
        Assert.Equal([ReminderKind.EventTomorrow], await prefs.GetEnabledAsync());
        Assert.Empty(await prefs.GetWantedAsync());
        Assert.Empty(await prefs.ApplyWantedAsync());
    }

    [Fact]
    public async Task ADecisionMadeLater_ReplacesAWishForTheSameKind()
    {
        var prefs = new NotificationPreferences(new InMemoryKeyValueStore());
        await prefs.AddWantedAsync(ReminderKind.EventTomorrow);
        await prefs.AddWantedAsync(ReminderKind.DrawNotDone);

        await prefs.SetEnabledAsync(ReminderKind.EventTomorrow, false);

        Assert.Equal([ReminderKind.DrawNotDone], await prefs.GetWantedAsync());
        Assert.Equal([ReminderKind.DrawNotDone], await prefs.ApplyWantedAsync());
        Assert.Equal([ReminderKind.DrawNotDone], await prefs.GetEnabledAsync());
    }

    [Fact]
    public async Task EnablingAndDisabling_RoundTrips()
    {
        var prefs = new NotificationPreferences(new InMemoryKeyValueStore());

        await prefs.SetEnabledAsync(ReminderKind.EventTomorrow, true);
        Assert.True(await prefs.IsEnabledAsync(ReminderKind.EventTomorrow));

        await prefs.SetEnabledAsync(ReminderKind.EventTomorrow, false);
        Assert.False(await prefs.IsEnabledAsync(ReminderKind.EventTomorrow));
    }

    [Fact]
    public async Task MultipleKinds_AreTrackedIndependently()
    {
        var prefs = new NotificationPreferences(new InMemoryKeyValueStore());

        await prefs.SetEnabledAsync(ReminderKind.DrawNotDone, true);
        await prefs.SetEnabledAsync(ReminderKind.MyWishlistEmpty, true);

        var enabled = await prefs.GetEnabledAsync();
        Assert.Contains(ReminderKind.DrawNotDone, enabled);
        Assert.Contains(ReminderKind.MyWishlistEmpty, enabled);
        Assert.DoesNotContain(ReminderKind.EventTomorrow, enabled);
    }

    [Fact]
    public async Task Choices_Persist_AcrossANewInstance_OnTheSameStore()
    {
        var store = new InMemoryKeyValueStore();
        await new NotificationPreferences(store).SetEnabledAsync(ReminderKind.DrawNotDone, true);

        // A fresh instance over the same store (mirrors reopening the app) still sees the choice.
        Assert.True(await new NotificationPreferences(store).IsEnabledAsync(ReminderKind.DrawNotDone));
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
