using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// The one-time import of the old model's groups (ADR-0086, NFR-001): a device that organized groups before the
/// change keeps them as groups on this device; copies it only took part in, or held through a join link, stay
/// behind; and the import runs once.
/// </summary>
public sealed class GroupRecordStoreImportTests
{
    private readonly InMemoryKeyValueStore _keyValues = new();

    [Fact]
    public async Task TheOldestShape_ABareArray_BecomesGroupsOnThisDevice()
    {
        var family = State("Family", "Anna", "Ben");
        var friends = State("Friends", "Cleo");
        await LegacyAsync($"[{GroupDocument.Serialize(family)},{GroupDocument.Serialize(friends)}]");

        var records = (await new GroupRecordStore(_keyValues).LoadAsync()).Records;

        Assert.Equal(["Family", "Friends"], records.Select(r => r.Document!.Name));
        Assert.All(records, r => Assert.Equal(GroupPlace.Device, r.Place));
        Assert.Equal(2, records.Single(r => r.Id == family.Id).Document!.Members.Count);
    }

    [Fact]
    public async Task OwnCopiesAreImported_JoinedAndJoinOnlyCopiesAreNot()
    {
        var own = State("On this device");
        var account = State("In my account");
        var admin = State("Shared, I organize");
        var joined = State("Joined");
        var joinOnly = State("Join link only");
        await LegacyAsync($$"""
            { "schemaVersion": 3, "records": [
              { "state": {{GroupDocument.Serialize(own)}}, "version": 1, "dirty": false },
              { "state": {{GroupDocument.Serialize(account)}}, "version": 3, "accountScope": "{{Guid.NewGuid()}}", "dirty": true, "mode": 2 },
              { "state": {{GroupDocument.Serialize(admin)}}, "version": 1, "dirty": false, "mode": 1, "tokens": { "adminToken": "a", "joinToken": "j" } },
              { "state": {{GroupDocument.Serialize(joined)}}, "version": 1, "dirty": false, "mode": 1, "claimedMemberId": "{{Guid.NewGuid()}}", "tokens": { "joinToken": "j" } },
              { "state": {{GroupDocument.Serialize(joinOnly)}}, "version": 1, "dirty": false, "mode": 1, "tokens": { "joinToken": "j" } }
            ] }
            """);

        var records = (await new GroupRecordStore(_keyValues).LoadAsync()).Records;

        Assert.Equal(["On this device", "In my account", "Shared, I organize"], records.Select(r => r.Document!.Name));
        Assert.All(records, r => Assert.Equal(GroupPlace.Device, r.Place));
    }

    [Fact]
    public async Task TheImportRunsOnce_AndRemovesTheOldKey()
    {
        await LegacyAsync($"[{GroupDocument.Serialize(State("Family"))}]");
        var store = new GroupRecordStore(_keyValues);

        await store.LoadAsync();
        await store.SaveAsync(GroupRecordsDocument.Empty);

        Assert.Empty((await store.LoadAsync()).Records);
        Assert.Null(await _keyValues.GetAsync(StorageScope.Durable, GroupRecordStore.LegacyStorageKey));
    }

    [Fact]
    public async Task ADamagedDocument_IsSetAside_BeforeAnythingOverwritesIt()
    {
        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.StorageKey, "{ damaged");
        var store = new GroupRecordStore(_keyValues);

        Assert.Empty((await store.LoadAsync()).Records);
        await store.SaveAsync(GroupRecordsDocument.Empty);

        Assert.Equal("{ damaged", await _keyValues.GetAsync(StorageScope.Durable, GroupRecordStore.UnreadableStorageKey));
    }

    [Fact]
    public async Task ADocumentFromAnotherVersion_IsSetAside_AndTheFirstOneSetAsideIsNeverOverwritten()
    {
        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.StorageKey, """{ "schemaVersion": 99, "records": [] }""");
        var store = new GroupRecordStore(_keyValues);
        await store.LoadAsync();

        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.StorageKey, "{ later damage");
        await store.LoadAsync();

        Assert.Equal("""{ "schemaVersion": 99, "records": [] }""", await _keyValues.GetAsync(StorageScope.Durable, GroupRecordStore.UnreadableStorageKey));
    }

    [Fact]
    public async Task AnUnreadableOldDocument_StartsEmpty()
    {
        await LegacyAsync("{ not json");

        Assert.Empty((await new GroupRecordStore(_keyValues).LoadAsync()).Records);
    }

    private static GroupState State(string name, params string[] members)
    {
        var group = Group.Create(name);
        foreach (var member in members)
        {
            group.AddMember(member);
        }

        return group.ToState();
    }

    private async Task LegacyAsync(string json) =>
        await _keyValues.SetAsync(StorageScope.Durable, GroupRecordStore.LegacyStorageKey, json);
}
