using System.Text.Json;
using AppSuite.SecretSanta.Core.Persistence;
using AppSuite.SecretSanta.LocalStore.Test.TestSupport;
using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="LocalSyncStore"/> — the persistence gateway and the in-place migration of the
/// pre-sync storage shape (FEAT-004). No group may be lost when a device that stored groups before M3
/// signs in for the first time.
/// </summary>
public sealed class LocalSyncStoreTests
{
    [Fact]
    public async Task LoadGroups_MigratesLegacyArray_AsAccountFreeVersionOneRecords()
    {
        var store = new InMemoryKeyValueStore();
        // The pre-sync shape: a bare array of GroupState under the same key.
        var legacy = new List<GroupState> { SyncTestData.GroupWith("Family", "Alice"), SyncTestData.GroupWith("Friends", "Bob") };
        var legacyJson = JsonSerializer.Serialize(legacy, GroupStateJsonContext.Default.ListGroupState);
        await store.SetAsync(StorageScope.Durable, LocalSyncStore.GroupsKey, legacyJson, default);
        var local = new LocalSyncStore(store);

        var records = await local.LoadGroupsAsync(default);

        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(1, r.Version));
        Assert.All(records, r => Assert.Null(r.AccountScope));
        Assert.All(records, r => Assert.False(r.Dirty));
        Assert.Contains(records, r => r.State.Name == "Family");
    }

    [Fact]
    public async Task SaveThenLoadGroups_RoundTripsRecordsInTheNewShape()
    {
        var store = new InMemoryKeyValueStore();
        var local = new LocalSyncStore(store);
        var account = Guid.NewGuid();
        var records = new List<SyncedGroupRecord>
        {
            new(SyncTestData.GroupWith("A", "x"), Version: 4, AccountScope: account, Dirty: true),
        };

        await local.SaveGroupsAsync(records, default);
        var loaded = await local.LoadGroupsAsync(default);

        var record = Assert.Single(loaded);
        Assert.Equal(4, record.Version);
        Assert.Equal(account, record.AccountScope);
        Assert.True(record.Dirty);
    }

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
    public async Task SaveGroups_WhenStorageRefusesTheWrite_Throws()
    {
        var store = new InMemoryKeyValueStore { RefuseWrites = true };
        var local = new LocalSyncStore(store);

        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => local.SaveGroupsAsync([new SyncedGroupRecord(SyncTestData.GroupWith("A"), 1, null, true)], default));
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
}
