// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="LocalSyncStore"/> — the persistence gateway of the account sync (FEAT-004): the device
/// state, the profile and the preferences. A refused write is raised, never swallowed.
/// </summary>
public sealed class LocalSyncStoreTests
{
    [Fact]
    public async Task LoadState_MintsAStableDeviceId_WhenNoneStored()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());

        var first = await local.LoadStateAsync(default);
        await local.SaveStateAsync(first, default);
        var second = await local.LoadStateAsync(default);

        Assert.False(string.IsNullOrWhiteSpace(first.DeviceId));
        Assert.Equal(first.DeviceId, second.DeviceId);
    }

    [Fact]
    public async Task SaveState_WhenStorageRefusesTheWrite_Throws()
    {
        var store = new InMemoryKeyValueStore { RefuseWrites = true };
        var local = new LocalSyncStore(store);

        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => local.SaveStateAsync(new SyncState("device", []), default));
    }

    [Fact]
    public async Task LoadState_IgnoresTheGroupFieldsOfAnOlderDocument()
    {
        var store = new InMemoryKeyValueStore();
        await store.SetAsync(StorageScope.Durable, LocalSyncStore.StateKey,
            """{ "deviceId": "d1", "cursors": [], "tombstones": [{ "accountId": "11111111-1111-1111-1111-111111111111", "kind": "group", "id": "22222222-2222-2222-2222-222222222222", "version": 1 }], "retractions": [] }""");

        var state = await new LocalSyncStore(store).LoadStateAsync(default);

        Assert.Equal("d1", state.DeviceId);
    }

    [Fact]
    public async Task LoadProfile_IsNull_WhenNoneStored()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());

        Assert.Null(await local.LoadProfileAsync(default));
    }

    [Fact]
    public async Task SaveThenLoadProfile_RoundTripsTheRecord()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var account = Guid.NewGuid();

        await local.SaveProfileAsync(new SyncedProfileRecord(account, "Alice", "fr-CH", Dirty: true), default);
        var loaded = await local.LoadProfileAsync(default);

        Assert.NotNull(loaded);
        Assert.Equal(account, loaded!.AccountScope);
        Assert.Equal("Alice", loaded.DisplayName);
        Assert.Equal("fr-CH", loaded.Language);
        Assert.True(loaded.Dirty);
    }

    [Fact]
    public async Task SaveProfile_WhenStorageRefusesTheWrite_Throws()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore { RefuseWrites = true });

        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => local.SaveProfileAsync(new SyncedProfileRecord(Guid.NewGuid(), "A", "de-CH", Dirty: true), default));
    }

    [Fact]
    public async Task LoadPreferences_IsNull_WhenNoneStored()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());

        Assert.Null(await local.LoadPreferencesAsync(default));
    }

    [Fact]
    public async Task SaveThenLoadPreferences_RoundTripsTheRecord()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var account = Guid.NewGuid();
        var group = Guid.NewGuid();

        await local.SavePreferencesAsync(
            new SyncedPreferencesRecord(new PreferencesState([group]), Version: 3, AccountScope: account, Dirty: true), default);
        var loaded = await local.LoadPreferencesAsync(default);

        Assert.NotNull(loaded);
        Assert.Equal(account, loaded!.AccountScope);
        Assert.Equal(3, loaded.Version);
        Assert.True(loaded.Dirty);
        Assert.Equal(new[] { group }, loaded.State.PinnedGroupIds);
    }
}
