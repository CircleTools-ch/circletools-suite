using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.LocalStore.Test.TestSupport;

namespace AppSuite.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="SyncingGroupRepository"/> (FEAT-004): it serves the domain port exactly as the
/// plain repository did, and additionally bumps the version, flags the copy dirty on every edit, and
/// queues a tombstone when a claimed group is deleted — all with no network in the write path.
/// </summary>
public sealed class SyncingGroupRepositoryTests
{
    private static (SyncingGroupRepository Repo, LocalSyncStore Local) NewRepository()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        return (new SyncingGroupRepository(local), local);
    }

    private static Group GroupWith(string name, params string[] members)
    {
        var group = Group.Create(name);
        foreach (var member in members)
        {
            group.AddMember(member, Wishlist.Empty);
        }

        return group;
    }

    [Fact]
    public async Task Save_NewGroup_IsVersionOne_ScopedToNoAccount_AndDirty()
    {
        var (repo, local) = NewRepository();
        var group = GroupWith("Family", "Alice");

        await repo.SaveAsync(group, default);

        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal(1, record.Version);
        Assert.Null(record.AccountScope);
        Assert.True(record.Dirty);
        Assert.Equal("Family", record.State.Name);
    }

    [Fact]
    public async Task Save_ExistingGroup_BumpsVersion_AndPreservesAccountScope()
    {
        var account = Guid.NewGuid();
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var group = GroupWith("Family", "Alice");
        // Seed the group as already claimed and clean.
        await local.SaveGroupsAsync([new SyncedGroupRecord(group.ToState(), Version: 3, AccountScope: account, Dirty: false)], default);
        var repo = new SyncingGroupRepository(local);

        var reloaded = await repo.GetAsync(group.Id, default);
        reloaded!.Rename("Renamed");
        await repo.SaveAsync(reloaded, default);

        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal("Renamed", record.State.Name);
        Assert.Equal(4, record.Version);
        Assert.Equal(account, record.AccountScope);
        Assert.True(record.Dirty);
    }

    [Fact]
    public async Task ListAndGet_RoundTripThroughTheDomain()
    {
        var (repo, _) = NewRepository();
        await repo.SaveAsync(GroupWith("One", "A", "B"), default);
        var two = GroupWith("Two", "C");
        await repo.SaveAsync(two, default);

        var all = await repo.ListAsync(default);
        var fetched = await repo.GetAsync(two.Id, default);

        Assert.Equal(2, all.Count);
        Assert.Equal("Two", fetched!.Name);
    }

    [Fact]
    public async Task Delete_AccountFreeGroup_QueuesNoTombstone()
    {
        var (repo, local) = NewRepository();
        var group = GroupWith("Family", "Alice");
        await repo.SaveAsync(group, default);

        await repo.DeleteAsync(group.Id, default);

        Assert.Empty(await local.LoadGroupsAsync(default));
        var state = await local.LoadStateAsync(default);
        Assert.Empty(state.Tombstones);
    }

    [Fact]
    public async Task Delete_ClaimedGroup_QueuesATombstoneForThatAccount()
    {
        var account = Guid.NewGuid();
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var group = GroupWith("Family", "Alice");
        await local.SaveGroupsAsync([new SyncedGroupRecord(group.ToState(), Version: 5, AccountScope: account, Dirty: false)], default);
        var repo = new SyncingGroupRepository(local);

        await repo.DeleteAsync(group.Id, default);

        Assert.Empty(await local.LoadGroupsAsync(default));
        var state = await local.LoadStateAsync(default);
        var tombstone = Assert.Single(state.Tombstones);
        Assert.Equal(account, tombstone.AccountId);
        Assert.Equal(group.Id, tombstone.Id);
        Assert.Equal(5, tombstone.Version);
        Assert.Equal(SyncKinds.Group, tombstone.Kind);
    }

    /// <summary>
    /// The write path holds the only copy of an unsynced edit (NFR-001): a refused write must surface,
    /// never read as a save. The syncing repository keeps that guarantee.
    /// </summary>
    [Fact]
    public async Task Save_WhenStorageRefusesTheWrite_Throws()
    {
        var store = new InMemoryKeyValueStore { RefuseWrites = true };
        var repo = new SyncingGroupRepository(new LocalSyncStore(store));

        await Assert.ThrowsAsync<Ui.Abstractions.StorageUnavailableException>(
            () => repo.SaveAsync(GroupWith("Family", "Alice"), default));
    }
}
