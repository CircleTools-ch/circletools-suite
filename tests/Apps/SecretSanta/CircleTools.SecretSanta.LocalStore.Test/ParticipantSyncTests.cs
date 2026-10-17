using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// The participant side of a shared-by-link group (ADR-0080, FEAT-024, TP-024): a joined device keeps its
/// participant key, always adopts the server's view, never pushes the group, writes only its own entry,
/// can't turn the group into a local copy, can leave, and finds the groups its account joined elsewhere.
/// </summary>
public sealed class ParticipantSyncTests
{
    private static readonly Guid Account = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Join_KeepsTheParticipantKey_AndAnAccountJoinRecordsTheAccount()
    {
        var (api, links, state) = await SharedGroupAsync();
        var device = NewStore();
        var withAccount = NewStore();

        await Service(device, api).JoinAsync(links.JoinToken, state.Members[0].Id, PersonClaimKind.Device, "d", null);
        await Service(withAccount, api).JoinAsync(links.JoinToken, state.Members[1].Id, PersonClaimKind.Account, "d2", Account);

        var deviceRecord = await SingleAsync(device);
        Assert.False(string.IsNullOrEmpty(deviceRecord.Tokens!.ParticipantKey));
        Assert.Null(deviceRecord.JoinedAccount);
        Assert.Equal(Account, (await SingleAsync(withAccount)).JoinedAccount);
    }

    [Fact]
    public async Task AJoinedGroup_AdoptsTheServerView_EvenAtAnEqualVersion_AndNeverPushes()
    {
        var (api, links, state) = await SharedGroupAsync();
        var participant = NewStore();
        await Service(participant, api).JoinAsync(links.JoinToken, state.Members[0].Id, PersonClaimKind.Device, "d", null);
        var joined = await SingleAsync(participant);

        // A copy stored before the server trimmed it (here: a different name, marked dirty) at the same version.
        await participant.SaveGroupsAsync([joined with { State = joined.State with { Name = "Full old copy" }, Dirty = true }]);
        var serverBefore = api.ServerBody(state.Id);

        await Service(participant, api).SyncGroupAsync(state.Id);

        var record = await SingleAsync(participant);
        Assert.Equal("Family", record.State.Name); // the server's view replaced the stored copy
        Assert.False(record.Dirty);
        Assert.Equal(serverBefore, api.ServerBody(state.Id)); // nothing was pushed
    }

    [Fact]
    public async Task MakeLocal_IsRefused_ForAJoinedGroup()
    {
        var (api, links, state) = await SharedGroupAsync();
        var participant = NewStore();
        await Service(participant, api).JoinAsync(links.JoinToken, state.Members[0].Id, PersonClaimKind.Device, "d", null);

        await Service(participant, api).MakeLocalAsync(state.Id);

        var record = await SingleAsync(participant);
        Assert.Equal(StorageMode.SharedByLink, record.Mode);
        Assert.Null(record.Tokens!.AdminToken);
        Assert.NotNull(record.Tokens.JoinToken);
    }

    [Fact]
    public async Task WriteOwnEntry_ShowsItAtOnce_AndSendsIt()
    {
        var (api, links, state) = await SharedGroupAsync();
        var participant = NewStore();
        var me = state.Members[0].Id;
        var occasion = state.Events![0].Id;
        await Service(participant, api).JoinAsync(links.JoinToken, me, PersonClaimKind.Device, "d", null);

        var sent = await Service(participant, api).WriteOwnEntryAsync(
            state.Id, new ParticipantEntry("vegan", [new ParticipantWishlist(occasion, ["book", "tea"])]));

        Assert.True(sent);
        var (token, entry) = Assert.Single(api.WrittenEntries);
        Assert.Equal(links.JoinToken, token);
        Assert.Equal("vegan", entry.GiftNote);
        var record = await SingleAsync(participant);
        Assert.Null(record.PendingEntry);
    }

    [Fact]
    public async Task WriteOwnEntry_Offline_StaysPending_AndVisible_ThenIsSentOnTheNextSync()
    {
        var (api, links, state) = await SharedGroupAsync();
        var participant = NewStore();
        var me = state.Members[0].Id;
        var occasion = state.Events![0].Id;
        await Service(participant, api).JoinAsync(links.JoinToken, me, PersonClaimKind.Device, "d", null);

        api.Unreachable = true;
        var sent = await Service(participant, api).WriteOwnEntryAsync(
            state.Id, new ParticipantEntry("vegan", [new ParticipantWishlist(occasion, ["book"])]));

        Assert.False(sent);
        var pending = await SingleAsync(participant);
        Assert.NotNull(pending.PendingEntry);
        Assert.Equal("vegan", pending.State.Members.Single(m => m.Id == me).GiftNote);
        Assert.Equal(["book"], pending.State.Events![0].Wishlists.Single(w => w.MemberId == me).Items);

        api.Unreachable = false;
        await Service(participant, api).SyncGroupAsync(state.Id);

        Assert.Single(api.WrittenEntries);
        Assert.Null((await SingleAsync(participant)).PendingEntry);
    }

    [Fact]
    public async Task Leave_ReleasesTheClaim_AndRemovesTheGroup_ButNotWhileOffline()
    {
        var (api, links, state) = await SharedGroupAsync();
        var participant = NewStore();
        var me = state.Members[0].Id;
        await Service(participant, api).JoinAsync(links.JoinToken, me, PersonClaimKind.Device, "d", null);

        api.Unreachable = true;
        Assert.False(await Service(participant, api).LeaveAsync(state.Id));
        Assert.Single(await participant.LoadGroupsAsync()); // kept: the person must not believe they left

        api.Unreachable = false;
        Assert.True(await Service(participant, api).LeaveAsync(state.Id));
        Assert.Empty(await participant.LoadGroupsAsync());
        Assert.False(api.IsClaimed(state.Id, me));
    }

    [Fact]
    public async Task DiscoverJoined_AddsTheAccountsJoinedGroups_ShownOnlyForThatAccount()
    {
        var (api, links, state) = await SharedGroupAsync();
        api.Joined = [new JoinedSharedGroup(state.Id, links.JoinToken, state.Members[1].Id)];
        var otherDevice = NewStore();

        await Service(otherDevice, api).DiscoverJoinedAsync(Account);
        await Service(otherDevice, api).DiscoverJoinedAsync(Account); // idempotent

        var record = await SingleAsync(otherDevice);
        Assert.True(record.IsJoinOnly());
        Assert.Equal(Account, record.JoinedAccount);
        Assert.Equal(state.Members[1].Id, record.ClaimedMemberId);
        var described = await new ClaimableGroupsService(otherDevice).GetAllAsync();
        Assert.Equal(Account, Assert.Single(described).Owner); // hidden while signed out / for others
    }

    [Fact]
    public async Task AnOrganizerPushTheServerRewrote_IsAdoptedRightAway()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        await Service(organizer, api).ShareByLinkAsync(state.Id);
        var record = await SingleAsync(organizer);
        await organizer.SaveGroupsAsync([record with { State = record.State with { Name = "Family 2026" }, Version = record.Version + 1, Dirty = true }]);

        // The server keeps a participant's gift note the organizer's copy doesn't have.
        api.RewritePushes = body => body.Replace("\"name\":\"Family 2026\"", "\"name\":\"Family 2026 (merged)\"", StringComparison.Ordinal);
        await Service(organizer, api).SyncGroupAsync(state.Id);

        var after = await SingleAsync(organizer);
        Assert.Equal("Family 2026 (merged)", after.State.Name);
        Assert.False(after.Dirty);
    }

    [Fact]
    public async Task ShareOwned_SharesAndKeepsTheGroupInTheAccount()
    {
        // ADR-0081: a signed-in organizer's share is owned by their account in one step.
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);

        Assert.True(await Service(organizer, api).ShareOwnedAsync(Account, state.Id));

        var record = await SingleAsync(organizer);
        Assert.Equal(StorageMode.Account, record.Mode);
        Assert.Equal(Account, record.AdminAccount);
        Assert.Null(record.Tokens!.AdminToken);
        Assert.NotNull(record.Tokens.JoinToken);
        Assert.True(record.IsAdminClaimedShared());
    }

    [Fact]
    public async Task StopSharing_RemovesTheOnlineCopy_AndLeavesALocalGroup()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        await Service(organizer, api).ShareOwnedAsync(Account, state.Id);

        Assert.True(await Service(organizer, api).StopSharingAsync(state.Id));

        var record = await SingleAsync(organizer);
        Assert.Equal(StorageMode.OnThisDevice, record.Mode);
        Assert.Null(record.Tokens);
        Assert.Null(record.AdminAccount);
        Assert.Null(api.ServerVersion(state.Id)); // gone online
    }

    [Fact]
    public async Task TheOwner_ReleasesAJoinedPerson_WithoutAnAdminToken()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        await Service(organizer, api).ShareOwnedAsync(Account, state.Id);
        var joinToken = (await SingleAsync(organizer)).Tokens!.JoinToken!;
        var alice = state.Members[0].Id;
        await Service(NewStore(), api).JoinAsync(joinToken, alice, PersonClaimKind.Device, "d", null);

        Assert.True(await Service(organizer, api).ReleasePersonAsync(state.Id, alice));
        Assert.False(api.IsClaimed(state.Id, alice));
    }

    [Fact]
    public async Task ParticipantEntries_ReadAndApply_RoundTrip()
    {
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        var me = state.Members[0].Id;
        var occasion = state.Events![0].Id;

        var applied = ParticipantEntries.Apply(state, me, new ParticipantEntry("  no nuts ", [new ParticipantWishlist(occasion, ["a", " ", "b"])]));
        var read = ParticipantEntries.Read(applied, me);

        Assert.Equal("no nuts", read.GiftNote);
        Assert.Equal(["a", "b"], Assert.Single(read.Wishlists).Items);

        var cleared = ParticipantEntries.Apply(applied, me, new ParticipantEntry("", [new ParticipantWishlist(occasion, [])]));
        Assert.Null(cleared.Members.Single(m => m.Id == me).GiftNote);
        Assert.Empty(ParticipantEntries.Read(cleared, me).Wishlists);
    }

    private static async Task<(FakeSharedGroupApi Api, MintedLinks Links, Core.Persistence.GroupState State)> SharedGroupAsync()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = WithEvent(SyncTestData.GroupWith("Family", "Alice", "Bruno"));
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        var links = await Service(organizer, api).ShareByLinkAsync(state.Id);
        return (api, links!, state);
    }

    private static Core.Persistence.GroupState WithEvent(Core.Persistence.GroupState state)
    {
        var group = Core.Domain.Group.FromState(state);
        group.AddEvent("Christmas");
        return group.ToState();
    }

    private static SharedSyncService Service(LocalSyncStore store, FakeSharedGroupApi api) => new(store, api, api);

    private static LocalSyncStore NewStore() => new(new InMemoryKeyValueStore());

    private static async Task<SyncedGroupRecord> SingleAsync(LocalSyncStore store) =>
        Assert.Single(await store.LoadGroupsAsync());
}
