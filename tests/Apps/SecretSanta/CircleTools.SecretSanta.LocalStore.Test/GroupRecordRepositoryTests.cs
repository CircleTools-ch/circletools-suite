using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// This device's group records under the ADR-0086 model: new groups start on this device, only a holder of the
/// write permission edits an online document, and deleting an online group queues the server delete.
/// </summary>
public sealed class GroupRecordRepositoryTests
{
    private readonly InMemoryKeyValueStore _keyValues = new();
    private readonly GroupRecordStore _store;
    private readonly GroupRecordRepository _repository;

    public GroupRecordRepositoryTests()
    {
        _store = new GroupRecordStore(_keyValues);
        _repository = new GroupRecordRepository(_store);
    }

    [Fact]
    public async Task ANewGroup_StartsOnThisDevice_AndEditsThereAreNeverDirty()
    {
        var group = Group.Create("Family");
        await _repository.SaveAsync(group, CancellationToken.None);
        group.Rename("Family 2026");
        await _repository.SaveAsync(group, CancellationToken.None);

        var record = Assert.Single((await _store.LoadAsync()).Records);
        Assert.Equal(GroupPlace.Device, record.Place);
        Assert.False(record.Dirty);
        Assert.Equal("Family 2026", (await _repository.GetAsync(group.Id, CancellationToken.None))!.Name);
    }

    [Fact]
    public async Task AnOnlineEdit_IsDirty_OnlyWithTheWritePermission()
    {
        var group = Group.Create("Family");
        await SeedOnlineAsync(group, [OnlinePermissions.ReadDocument, OnlinePermissions.WriteDocument]);

        group.Rename("Renamed");
        await _repository.SaveAsync(group, CancellationToken.None);
        Assert.True(Assert.Single((await _store.LoadAsync()).Records).Dirty);

        await SeedOnlineAsync(group, [OnlinePermissions.ReadDocument]);
        await Assert.ThrowsAsync<GroupReadOnlyException>(() => _repository.SaveAsync(group, CancellationToken.None));
    }

    [Fact]
    public async Task DeletingAnOnlineGroup_QueuesTheServerDelete_OnlyWithTheDeletePermission()
    {
        var group = Group.Create("Family");
        await SeedOnlineAsync(group, [OnlinePermissions.DeleteGroup]);

        await _repository.DeleteAsync(group.Id, CancellationToken.None);

        var document = await _store.LoadAsync();
        Assert.Empty(document.Records);
        var removal = Assert.Single(document.Removals);
        Assert.False(removal.Leave);

        var other = Group.Create("Other");
        await SeedOnlineAsync(other, [OnlinePermissions.ReadDocument]);
        await _repository.DeleteAsync(other.Id, CancellationToken.None);
        Assert.Single((await _store.LoadAsync()).Removals);
    }

    [Fact]
    public async Task AParticipantRecord_HasNoDocumentForTheDomain()
    {
        var access = new OnlineAccess(AccessPrincipal.DeviceKey, "participant", [OnlinePermissions.ReadOwnView], Secret: "key");
        var id = Guid.NewGuid();
        await _store.SaveAsync(GroupRecordsDocument.Empty with { Records = [new GroupRecord(id, GroupPlace.Online, null, null, 3, false, access)] });

        Assert.Null(await _repository.GetAsync(id, CancellationToken.None));
        Assert.Empty(await _repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public void AnAccountsGroup_IsVisibleOnlyToThatAccount()
    {
        var mine = Guid.NewGuid();
        var access = new OnlineAccess(AccessPrincipal.Account, "owner", [], AccountId: mine);
        var record = new GroupRecord(Guid.NewGuid(), GroupPlace.Online, null, null, 1, false, access);

        Assert.True(record.IsVisibleTo(mine));
        Assert.False(record.IsVisibleTo(Guid.NewGuid()));
        Assert.False(record.IsVisibleTo(null));
        Assert.True((record with { Access = access with { Principal = AccessPrincipal.DeviceKey } }).IsVisibleTo(null));
    }

    [Fact]
    public async Task AnUnreadableOrOlderStore_LoadsEmpty()
    {
        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.StorageKey, "{not json");
        Assert.Empty((await _store.LoadAsync()).Records);

        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.StorageKey, """{"schemaVersion":3,"records":[],"removals":[],"invitations":[]}""");
        Assert.Equal(GroupRecordsDocument.CurrentSchemaVersion, (await _store.LoadAsync()).SchemaVersion);
    }

    private async Task SeedOnlineAsync(Group group, IReadOnlyList<string> permissions)
    {
        var access = new OnlineAccess(AccessPrincipal.Account, "owner", permissions, AccountId: Guid.NewGuid());
        var document = await _store.LoadAsync();
        await _store.SaveAsync(document with
        {
            Records = [.. document.Records.Where(r => r.Id != group.Id), new GroupRecord(group.Id, GroupPlace.Online, group.ToState(), null, 1, false, access)],
        });
    }
}
