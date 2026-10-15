using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// The client shared-by-link engine (FEAT-013): the upgrade (share by link), the reconcile (pull the
/// server when newer, push a dirty local edit), the downgrade (make local, keep the copy), admin-link
/// rotation, and the fall-back to on-this-device when the online copy is gone.
/// </summary>
public sealed class SharedSyncServiceTests
{
    private static SharedSyncService Service(LocalSyncStore store, FakeSharedGroupApi api) => new(store, api, api);

    [Fact]
    public async Task ShareByLink_UploadsTheGroup_AndRecordsItsLinks()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");

        var links = await Service(store, api).ShareByLinkAsync(groupId);

        Assert.NotNull(links);
        var record = await SingleAsync(store);
        Assert.Equal(StorageMode.SharedByLink, record.Mode);
        Assert.Equal(links!.AdminToken, record.Tokens!.AdminToken);
        Assert.Equal(links.JoinToken, record.Tokens.JoinToken);
        Assert.False(record.Dirty);
        Assert.Equal(record.Version, api.ServerVersion(groupId));
    }

    [Fact]
    public async Task ShareByLink_IsIdempotent()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        var service = Service(store, api);

        var first = await service.ShareByLinkAsync(groupId);
        var second = await service.ShareByLinkAsync(groupId);

        Assert.Equal(first!.AdminToken, second!.AdminToken);
        Assert.Equal(first.JoinToken, second.JoinToken);
    }

    [Fact]
    public async Task ShareByLink_WhenServerUnreachable_StaysLocal()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi { Unreachable = true };
        var groupId = await SeedLocalGroupAsync(store, "Family");

        var links = await Service(store, api).ShareByLinkAsync(groupId);

        Assert.Null(links);
        Assert.Equal(StorageMode.OnThisDevice, (await SingleAsync(store)).Mode);
    }

    [Fact]
    public async Task ShareByLink_RetryAfterAnUnreachableServer_ReusesTheSameCreatorKey()
    {
        // The creator key is minted and persisted before the create call, so a lost-response retry
        // presents the same key — the server-side gate then hands the links back rather than refusing
        // a caller it cannot tell apart from a join-link holder (ADR-0058 security note).
        var store = NewStore();
        var api = new FakeSharedGroupApi { Unreachable = true };
        var groupId = await SeedLocalGroupAsync(store, "Family");

        var failed = await Service(store, api).ShareByLinkAsync(groupId);
        Assert.Null(failed);
        var afterFailure = await SingleAsync(store);
        Assert.False(string.IsNullOrEmpty(afterFailure.Tokens?.CreatorKey)); // persisted before the call

        api.Unreachable = false;
        var links = await Service(store, api).ShareByLinkAsync(groupId);

        Assert.NotNull(links);
        Assert.Equal(afterFailure.Tokens!.CreatorKey, api.CreatorKeyFor(groupId)); // same key on the retry
        Assert.Equal(afterFailure.Tokens.CreatorKey, (await SingleAsync(store)).Tokens!.CreatorKey); // kept on the record
    }

    [Fact]
    public async Task SyncGroup_PullsAServerCopyThatIsNewer()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);

        // Another device pushed a renamed body at a higher version.
        var renamed = (await SingleAsync(store)).State with { Name = "Family 2026" };
        api.SeedServer(groupId, JsonSerializer.Serialize(renamed, SyncJsonContext.Default.GroupState), version: 9);

        await Service(store, api).SyncGroupAsync(groupId);

        var record = await SingleAsync(store);
        Assert.Equal(9, record.Version);
        Assert.Equal("Family 2026", record.State.Name);
        Assert.False(record.Dirty);
    }

    [Fact]
    public async Task SyncGroup_PushesADirtyLocalEdit()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);

        // Simulate a local edit: bump version and mark dirty.
        var record = await SingleAsync(store);
        await store.SaveGroupsAsync([record with { Version = record.Version + 3, Dirty = true }]);

        await Service(store, api).SyncGroupAsync(groupId);

        var after = await SingleAsync(store);
        Assert.False(after.Dirty);
        Assert.Equal(after.Version, api.ServerVersion(groupId));
    }

    [Fact]
    public async Task MakeLocal_RemovesTheServerCopy_ButKeepsTheLocalOne()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);

        await Service(store, api).MakeLocalAsync(groupId);

        var record = await SingleAsync(store);
        Assert.Equal(StorageMode.OnThisDevice, record.Mode);
        Assert.Null(record.Tokens);
        Assert.Equal("Family", record.State.Name); // local copy kept
        Assert.Null(api.ServerVersion(groupId)); // server copy gone
    }

    [Fact]
    public async Task SyncGroup_WhenTheServerCopyIsGone_FallsBackToOnThisDevice()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        var links = await Service(store, api).ShareByLinkAsync(groupId);

        // The online copy is deleted elsewhere (or expired).
        await api.DeleteAsync(links!.AdminToken);

        await Service(store, api).SyncGroupAsync(groupId);

        var record = await SingleAsync(store);
        Assert.Equal(StorageMode.OnThisDevice, record.Mode);
        Assert.Null(record.Tokens);
        Assert.Equal("Family", record.State.Name); // local copy survives
    }

    [Fact]
    public async Task RotateAdmin_ReplacesTheStoredAdminToken()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        var links = await Service(store, api).ShareByLinkAsync(groupId);

        var fresh = await Service(store, api).RotateAdminAsync(groupId);

        Assert.NotNull(fresh);
        Assert.NotEqual(links!.AdminToken, fresh);
        var record = await SingleAsync(store);
        Assert.Equal(fresh, record.Tokens!.AdminToken);
        Assert.Equal(links.JoinToken, record.Tokens.JoinToken); // join link unchanged
    }

    [Fact]
    public async Task Join_ClaimsTheMember_AndAddsTheGroupWithThatIdentity()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = SyncTestData.GroupWith("Family", "Alice", "Bruno");
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        var links = await new SharedSyncService(organizer, api, api).ShareByLinkAsync(state.Id);
        var aliceId = state.Members[0].Id;

        var participant = NewStore();
        var result = await new SharedSyncService(participant, api, api)
            .JoinAsync(links!.JoinToken, aliceId, PersonClaimKind.Device, "device-2", null);

        Assert.Equal(JoinStatus.Joined, result.Status);
        Assert.Equal(state.Id, result.GroupId);
        var record = (await participant.LoadGroupsAsync()).Single();
        Assert.Equal(StorageMode.SharedByLink, record.Mode);
        Assert.Equal(aliceId, record.ClaimedMemberId);
        Assert.Equal(links.JoinToken, record.Tokens!.JoinToken);
        Assert.Null(record.Tokens.AdminToken); // a joiner holds only the join link
    }

    [Fact]
    public async Task Join_TheSameMemberTwice_IsRejected_AndAddsNothing()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var state = SyncTestData.GroupWith("Family", "Alice", "Bruno");
        await organizer.SaveGroupsAsync([new SyncedGroupRecord(state, 1, null, false)]);
        var links = await new SharedSyncService(organizer, api, api).ShareByLinkAsync(state.Id);
        var aliceId = state.Members[0].Id;

        var first = NewStore();
        await new SharedSyncService(first, api, api).JoinAsync(links!.JoinToken, aliceId, PersonClaimKind.Device, "device-a", null);

        var second = NewStore();
        var result = await new SharedSyncService(second, api, api).JoinAsync(links.JoinToken, aliceId, PersonClaimKind.Device, "device-b", null);

        Assert.Equal(JoinStatus.AlreadyClaimed, result.Status);
        Assert.Empty(await second.LoadGroupsAsync()); // nothing added when the claim is refused
    }

    [Fact]
    public async Task ClaimAdminToAccount_MarksTheGroupAccountOwned_AndDropsTheAdminToken()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);

        var ok = await Service(store, api).ClaimAdminToAccountAsync(Account, groupId);

        Assert.True(ok);
        var record = await SingleAsync(store);
        Assert.Equal(StorageMode.Account, record.Mode);
        Assert.Equal(Account, record.AdminAccount); // recorded, so it is hidden signed out (ADR-0078)
        Assert.Null(record.AccountScope); // it lives in the shared-group store, not the account store
        Assert.Null(record.Tokens!.AdminToken); // the admin link is retired
        Assert.NotNull(record.Tokens.JoinToken); // the join link keeps working
    }

    [Fact]
    public async Task ReleaseToLink_ReturnsAnAccountGroupToSharedByLink_WithAFreshAdminToken()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);
        await Service(store, api).ClaimAdminToAccountAsync(Account, groupId);

        var ok = await Service(store, api).ReleaseToLinkAsync(groupId);

        Assert.True(ok);
        var record = await SingleAsync(store);
        Assert.Equal(StorageMode.SharedByLink, record.Mode);
        Assert.Null(record.AdminAccount); // account-free again
        Assert.NotNull(record.Tokens!.AdminToken); // a fresh admin link
    }

    [Fact]
    public async Task AdoptAdmin_GivesACoOrganizerTheGroupWithAdminCapability()
    {
        var api = new FakeSharedGroupApi();
        var organizer = NewStore();
        var groupId = await SeedLocalGroupAsync(organizer, "Family");
        var links = await Service(organizer, api).ShareByLinkAsync(groupId);

        var coOrganizer = NewStore();
        var adopted = await Service(coOrganizer, api).AdoptAdminAsync(links!.AdminToken);

        Assert.Equal(groupId, adopted);
        var record = (await coOrganizer.LoadGroupsAsync()).Single();
        Assert.Equal(StorageMode.SharedByLink, record.Mode);
        Assert.Equal(links.AdminToken, record.Tokens!.AdminToken);
    }

    [Fact]
    public async Task SyncGroup_ForAClaimedGroup_PushesADirtyEditViaTheOwnerPath()
    {
        var store = NewStore();
        var api = new FakeSharedGroupApi();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);
        await Service(store, api).ClaimAdminToAccountAsync(Account, groupId); // now My account mode (owner path)

        // A local edit while account-owned.
        var record = await SingleAsync(store);
        await store.SaveGroupsAsync([record with { Version = record.Version + 2, Dirty = true }]);

        await Service(store, api).SyncGroupAsync(groupId);

        var after = await SingleAsync(store);
        Assert.Equal(StorageMode.Account, after.Mode);
        Assert.False(after.Dirty); // pushed via the owner path
        Assert.Equal(after.Version, api.ServerVersion(groupId));
    }

    [Fact]
    public async Task MergeOwned_DiscoversAGroupClaimedOnAnotherDevice()
    {
        var api = new FakeSharedGroupApi();
        var deviceA = NewStore();
        var groupId = await SeedLocalGroupAsync(deviceA, "Family");
        await Service(deviceA, api).ShareByLinkAsync(groupId);
        await Service(deviceA, api).ClaimAdminToAccountAsync(Account, groupId); // claimed on device A → owned server-side

        // Device B (same account) has never seen the group; discovery adds it as a My-account group.
        var deviceB = NewStore();
        var listed = await Service(deviceB, api).MergeOwnedAsync(Account);

        Assert.True(listed);
        var record = (await deviceB.LoadGroupsAsync()).Single();
        Assert.Equal(groupId, record.State.Id);
        Assert.Equal(StorageMode.Account, record.Mode);
        Assert.Equal(Account, record.AdminAccount);
        Assert.Null(record.AccountScope);
        Assert.Equal("Family", record.State.Name);
    }

    [Fact]
    public async Task MergeOwned_AttributesAHeldLegacyRecord_SoTheAutoClaimLeavesItAlone()
    {
        // ADR-0078: a "My account" record written before its account was recorded is, if the account owns
        // it as a shared group, attributed as admin-claimed - otherwise the auto-claim would upload it into
        // the account sync store a second time.
        var api = new FakeSharedGroupApi();
        var store = NewStore();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);
        await Service(store, api).ClaimAdminToAccountAsync(Account, groupId);
        var claimed = await SingleAsync(store);
        await store.SaveGroupsAsync([claimed with { AdminAccount = null }]); // as written by an older build

        await Service(store, api).MergeOwnedAsync(Account);

        var record = await SingleAsync(store);
        Assert.Equal(Account, record.AdminAccount);
        var awaiting = await new ClaimableGroupsService(store).GetAwaitingClaimAsync(Account, includeUnattributed: true);
        Assert.Empty(awaiting);
    }

    [Fact]
    public async Task DeletingAnAdminClaimedGroup_DoesNotComeBack_AndItsOnlineCopyIsDeleted()
    {
        // ADR-0078 (Android + iOS): deleting a "My account" group made it reappear — the delete was local
        // only, and the next owned-group discovery downloaded the server copy again.
        var api = new FakeSharedGroupApi();
        var store = NewStore();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);
        await Service(store, api).ClaimAdminToAccountAsync(Account, groupId);

        await new SyncingGroupRepository(store).DeleteAsync(groupId, default);

        // Still on the server (delete not sent yet, e.g. offline) — discovery must not bring it back.
        await Service(store, api).MergeOwnedAsync(Account);
        Assert.Empty(await store.LoadGroupsAsync());

        await Service(store, api).SendOwnedDeletesAsync(Account);

        Assert.Null(api.ServerVersion(groupId)); // the online copy is gone
        Assert.Empty((await store.LoadStateAsync()).OwnedDeletes ?? []); // and the queue drained
        await Service(store, api).MergeOwnedAsync(Account);
        Assert.Empty(await store.LoadGroupsAsync());
    }

    [Fact]
    public async Task SendOwnedDeletes_KeepsADeleteQueued_WhileTheServerIsUnreachable()
    {
        var api = new FakeSharedGroupApi();
        var store = NewStore();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        await Service(store, api).ShareByLinkAsync(groupId);
        await Service(store, api).ClaimAdminToAccountAsync(Account, groupId);
        await new SyncingGroupRepository(store).DeleteAsync(groupId, default);

        api.Unreachable = true;
        await Service(store, api).SendOwnedDeletesAsync(Account);

        var queued = Assert.Single((await store.LoadStateAsync()).OwnedDeletes ?? []);
        Assert.Equal(new PendingOwnedDelete(groupId, Account), queued);
    }

    [Fact]
    public async Task DeletingAGroupStillWaitingForItsClaim_QueuesNoOwnedDelete()
    {
        // Nothing of it is online yet, so there is nothing for the owner to delete.
        var store = NewStore();
        var groupId = await SeedLocalGroupAsync(store, "Family");
        var record = await SingleAsync(store);
        await store.SaveGroupsAsync([record with { Mode = StorageMode.Account, PendingClaimAccount = Account }]);

        await new SyncingGroupRepository(store).DeleteAsync(groupId, default);

        Assert.Empty((await store.LoadStateAsync()).OwnedDeletes ?? []);
    }

    private static readonly Guid Account = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static LocalSyncStore NewStore() => new(new InMemoryKeyValueStore());

    private static async Task<Guid> SeedLocalGroupAsync(LocalSyncStore store, string name)
    {
        var state = SyncTestData.GroupWith(name, "Alice");
        await store.SaveGroupsAsync([new SyncedGroupRecord(state, Version: 1, AccountScope: null, Dirty: false)]);
        return state.Id;
    }

    private static async Task<SyncedGroupRecord> SingleAsync(LocalSyncStore store) =>
        (await store.LoadGroupsAsync()).Single();
}
