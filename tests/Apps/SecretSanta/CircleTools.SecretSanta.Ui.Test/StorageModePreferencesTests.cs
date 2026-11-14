// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="StorageModePreferences"/> (FR-048, ADR-0071, ADR-0086): the default place for new
/// groups starts at "On this device", round-trips, a default stored by the older three-mode picker reads as online,
/// the one-time onboarding flag toggles, choices persist across app restarts, and a failing store degrades to a
/// safe default rather than throwing.
/// </summary>
public sealed class StorageModePreferencesTests
{
    [Fact]
    public async Task Default_IsOnThisDevice_OnAFreshStore()
    {
        var prefs = new StorageModePreferences(new InMemoryKeyValueStore());

        Assert.Equal(GroupPlace.Device, await prefs.GetDefaultAsync());
    }

    [Theory]
    [InlineData(GroupPlace.Device)]
    [InlineData(GroupPlace.Online)]
    public async Task SettingTheDefault_RoundTrips(GroupPlace place)
    {
        var prefs = new StorageModePreferences(new InMemoryKeyValueStore());

        await prefs.SetDefaultAsync(place);

        Assert.Equal(place, await prefs.GetDefaultAsync());
    }

    [Theory]
    [InlineData("SharedByLink")]
    [InlineData("Account")]
    public async Task AnOlderSharedOrAccountDefault_ReadsAsOnline(string stored)
    {
        var store = new InMemoryKeyValueStore();
        await store.SetAsync(StorageScope.Durable, "secretsanta.storage.default", stored);

        Assert.Equal(GroupPlace.Online, await new StorageModePreferences(store).GetDefaultAsync());
    }

    [Fact]
    public async Task Onboarded_IsFalse_ThenTrue_AfterMarking()
    {
        var prefs = new StorageModePreferences(new InMemoryKeyValueStore());

        Assert.False(await prefs.IsOnboardedAsync());

        await prefs.MarkOnboardedAsync();

        Assert.True(await prefs.IsOnboardedAsync());
    }

    [Fact]
    public async Task Choices_Persist_AcrossANewInstance_OnTheSameStore()
    {
        var store = new InMemoryKeyValueStore();
        await new StorageModePreferences(store).SetDefaultAsync(GroupPlace.Online);
        await new StorageModePreferences(store).MarkOnboardedAsync();

        // A fresh instance over the same store (mirrors reopening the app) still sees the choices.
        var reopened = new StorageModePreferences(store);
        Assert.Equal(GroupPlace.Online, await reopened.GetDefaultAsync());
        Assert.True(await reopened.IsOnboardedAsync());
    }

    [Fact]
    public async Task AFailingStore_DegradesSafely_WithoutThrowing()
    {
        var prefs = new StorageModePreferences(new ThrowingKeyValueStore());

        // Reads fall back to the local-first default; the onboarding is treated as done so a broken store
        // never traps the user in a dialog loop. Writes are best-effort and must not throw.
        Assert.Equal(GroupPlace.Device, await prefs.GetDefaultAsync());
        Assert.True(await prefs.IsOnboardedAsync());
        await prefs.SetDefaultAsync(GroupPlace.Online);
        await prefs.MarkOnboardedAsync();
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

    private sealed class ThrowingKeyValueStore : IKeyValueStore
    {
        public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
            throw new StorageUnavailableException("unavailable");

        public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default) =>
            throw new StorageUnavailableException("unavailable");

        public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
            throw new StorageUnavailableException("unavailable");
    }
}
