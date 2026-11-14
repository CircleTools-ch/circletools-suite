// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Two accounts on one device, both in the same group (ADR-0086): signing out keeps the first account's record
/// (hidden), and discovery used to add a group only when no record with its id existed — so the second account
/// never saw the group, even after a restart (ST-1, found on the Pixel 9 Pro, 2026-10-28). Now one record per group
/// is in play and the others wait in <see cref="GroupRecordsDocument.Parked"/> until their holder fits again.
/// </summary>
public sealed class AccountSwitchDiscoveryTests
{
    private static readonly string[] ParticipantPermissions =
        [OnlinePermissions.ReadOwnView, OnlinePermissions.WriteOwnEntry, OnlinePermissions.Leave];

    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();
    private readonly Guid _pixel = Guid.NewGuid();
    private readonly Guid _moll = Guid.NewGuid();

    [Fact]
    public async Task AGroupTheSignedOutAccountLeftHere_ShowsForTheNextAccountInIt()
    {
        var store = OnlineRecords.Seed(new MemoryKeyValueStore(), TakingPart(_first, _pixel));

        await new OnlineGroupSync(store, ApiFor(_moll, "Moll")).SyncAsync(_second);

        var record = Assert.Single((await store.LoadAsync()).Records);
        Assert.True(record.IsVisibleTo(_second));
        Assert.Equal(_second, record.Access!.AccountId);
        Assert.Equal(_moll, record.Access.MemberId);
    }

    [Fact]
    public async Task TheFirstAccount_TakesTheGroupBack_WhenItSignsInAgain()
    {
        var store = OnlineRecords.Seed(new MemoryKeyValueStore(), TakingPart(_first, _pixel));
        await new OnlineGroupSync(store, ApiFor(_moll, "Moll")).SyncAsync(_second);

        await new OnlineGroupSync(store, ApiFor(_pixel, "Pixel")).SyncAsync(_first);

        var record = Assert.Single((await store.LoadAsync()).Records);
        Assert.Equal(_first, record.Access!.AccountId);
        Assert.Equal(_pixel, record.Access.MemberId);
    }

    [Fact]
    public async Task ARecordWithAnUnsentEntry_WaitsForItsAccount_AndComesBackWithIt()
    {
        var pending = new OwnEntryRequest("Vegan", []);
        var store = OnlineRecords.Seed(new MemoryKeyValueStore(), TakingPart(_first, _pixel) with { PendingEntry = pending });

        await new OnlineGroupSync(store, ApiFor(_moll, "Moll")).SyncAsync(_second);
        var meanwhile = await store.LoadAsync();
        Assert.Equal(_second, Assert.Single(meanwhile.Records).Access!.AccountId);

        await new OnlineGroupSync(store, ApiFor(_pixel, "Pixel")).SyncAsync(_first);

        var back = await store.LoadAsync();
        var record = Assert.Single(back.Records);
        Assert.Equal(_first, record.Access!.AccountId);
        Assert.Equal("Vegan", record.PendingEntry?.GiftNote);
        Assert.Equal(_second, Assert.Single(back.Parked!).Access!.AccountId);
    }

    [Fact]
    public async Task AGroupThisDeviceJoinedWithoutAnAccount_GivesWayToTheSignedInAccountsOwnRole_AndComesBackSignedOut()
    {
        var store = OnlineRecords.Seed(new MemoryKeyValueStore(), JoinedWithoutAccount(_pixel));

        await new OnlineGroupSync(store, ApiFor(_moll, "Moll")).SyncAsync(_second);

        var signedIn = await store.LoadAsync();
        Assert.Equal(_moll, Assert.Single(signedIn.Records).Access!.MemberId);
        Assert.Equal(AccessPrincipal.DeviceKey, Assert.Single(signedIn.Parked!).Access!.Principal);

        await new OnlineGroupSync(store, new FakeOnlineGroupApi()).SyncAsync(null);

        var signedOut = await store.LoadAsync();
        var record = Assert.Single(signedOut.Records);
        Assert.True(record.IsVisibleTo(null));
        Assert.Equal(_pixel, record.Access!.MemberId);
        Assert.Equal("device-key", record.Access.Secret);
    }

    [Fact]
    public async Task AGroupThisDeviceOrganizesByLink_StaysInPlay_WhenTheSignedInAccountOnlyTakesPart()
    {
        var organizing = new GroupRecord(_groupId, GroupPlace.Online, new GroupState(_groupId, "QA", null, null, [], [], [], Events: []), null, 3, false,
            new OnlineAccess(AccessPrincipal.OrganizerLink, "owner", [OnlinePermissions.ReadDocument, OnlinePermissions.WriteDocument], Secret: "organizer"));
        var store = OnlineRecords.Seed(new MemoryKeyValueStore(), organizing);

        await new OnlineGroupSync(store, ApiFor(_moll, "Moll")).SyncAsync(_second);

        var document = await store.LoadAsync();
        Assert.Equal(AccessPrincipal.OrganizerLink, Assert.Single(document.Records).Access!.Principal);
        Assert.Equal(_second, Assert.Single(document.Parked!).Access!.AccountId);
    }

    private GroupRecord TakingPart(Guid account, Guid member) =>
        new(_groupId, GroupPlace.Online, null, null, 3, false,
            new OnlineAccess(AccessPrincipal.Account, "participant", ParticipantPermissions, AccountId: account, MemberId: member));

    private GroupRecord JoinedWithoutAccount(Guid member) =>
        new(_groupId, GroupPlace.Online, null, null, 3, false,
            new OnlineAccess(AccessPrincipal.DeviceKey, "participant", ParticipantPermissions, MemberId: member, Secret: "device-key"));

    private FakeOnlineGroupApi ApiFor(Guid member, string name) => new()
    {
        MyGroups = new MyGroups([new MyGroup(_groupId, "QA", "participant", false, member, name, 3, ParticipantPermissions)]),
    };

    private sealed class MemoryKeyValueStore : IKeyValueStore
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
