using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for the <see cref="SyncService"/> cycle (FEAT-004): what it pushes, how it applies the
/// server delta, and — the properties that fail silently — that it clears nothing it should not and
/// advances the cursor only on a completed cycle (NFR-008 convergence and data safety).
/// </summary>
public sealed class SyncServiceTests
{
    private static (SyncService Service, LocalSyncStore Local, FakeSyncApi Api) NewService()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var api = new FakeSyncApi();
        return (new SyncService(local, api), local, api);
    }

    private static async Task SeedAsync(LocalSyncStore local, params SyncedGroupRecord[] records) =>
        await local.SaveGroupsAsync(records, default);

    [Fact]
    public async Task NoOpCycle_SendsNoAggregateBodiesOrTombstones()
    {
        var (service, _, api) = NewService();

        var outcome = await service.SyncNowAsync(Guid.NewGuid(), default);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(api.LastRequest!.Changes);
        Assert.Empty(api.LastRequest.Tombstones);
        Assert.Equal(0, outcome.Pushed);
        Assert.Equal(0, outcome.Pulled);
    }

    [Fact]
    public async Task DirtyClaimedGroup_IsPushedWithFullBodyAndVersion_ThenMarkedSynced()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Family", "Alice");
        await SeedAsync(local, new SyncedGroupRecord(state, Version: 2, AccountScope: account, Dirty: true));
        api.Returns(new SyncResponse(Cursor: 7, [], [], [], []));

        var outcome = await service.SyncNowAsync(account, default);

        var change = Assert.Single(api.LastRequest!.Changes);
        Assert.Equal(state.Id, change.Id);
        Assert.Equal(2, change.Version);
        Assert.Equal(SyncKinds.Group, change.Kind);
        Assert.Equal(1, outcome.Pushed);

        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.False(record.Dirty);
        var syncState = await local.LoadStateAsync(default);
        Assert.Equal(7, syncState.CursorFor(account));
    }

    [Fact]
    public async Task AccountFreeGroup_IsNeverPushed()
    {
        var (service, local, api) = NewService();
        await SeedAsync(local, new SyncedGroupRecord(SyncTestData.GroupWith("Local", "A"), 3, AccountScope: null, Dirty: true));

        await service.SyncNowAsync(Guid.NewGuid(), default);

        Assert.Empty(api.LastRequest!.Changes);
    }

    [Fact]
    public async Task DirtyPreferences_ArePushedAsPreferencesKind_ThenMarkedSynced()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var group = Guid.NewGuid();
        await local.SavePreferencesAsync(
            new SyncedPreferencesRecord(new PreferencesState([group]), Version: 1, AccountScope: account, Dirty: true), default);
        api.Returns(new SyncResponse(Cursor: 3, [], [], [], []));

        await service.SyncNowAsync(account, default);

        // The preferences document rides the same cycle under its own kind, keyed by the account id.
        var change = Assert.Single(api.LastRequest!.Changes);
        Assert.Equal(SyncKinds.Preferences, change.Kind);
        Assert.Equal(account, change.Id);
        Assert.Equal(1, change.Version);

        var stored = await local.LoadPreferencesAsync(default);
        Assert.NotNull(stored);
        Assert.False(stored!.Dirty);
        Assert.Equal(new[] { group }, stored.State.PinnedGroupIds);
    }

    [Fact]
    public async Task PreferencesForAnotherAccount_AreNotPushed()
    {
        var (service, local, api) = NewService();
        await local.SavePreferencesAsync(
            new SyncedPreferencesRecord(new PreferencesState([Guid.NewGuid()]), Version: 1, AccountScope: Guid.NewGuid(), Dirty: true), default);

        await service.SyncNowAsync(Guid.NewGuid(), default);

        Assert.Empty(api.LastRequest!.Changes);
    }

    [Fact]
    public async Task PulledPreferences_AreAppliedLocally()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var group = Guid.NewGuid();
        var body = JsonSerializer.SerializeToElement(new PreferencesState([group]), SyncJsonContext.Default.PreferencesState);
        api.Returns(new SyncResponse(Cursor: 5, [new SyncChange(SyncKinds.Preferences, account, 4, body)], [], [], []));

        await service.SyncNowAsync(account, default);

        var stored = await local.LoadPreferencesAsync(default);
        Assert.NotNull(stored);
        Assert.False(stored!.Dirty);
        Assert.Equal(4, stored.Version);
        Assert.Equal(new[] { group }, stored.State.PinnedGroupIds);
    }

    [Fact]
    public async Task GroupClaimedToAnotherAccount_IsNotPushedToTheAccountSigningIn()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var (service, local, api) = NewService();
        await SeedAsync(local, new SyncedGroupRecord(SyncTestData.GroupWith("A's group", "x"), 2, AccountScope: accountA, Dirty: true));

        await service.SyncNowAsync(accountB, default);

        Assert.Empty(api.LastRequest!.Changes);
    }

    [Fact]
    public async Task PulledChange_IsAppliedLocally_AsAScopedCleanCopy()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var incoming = SyncTestData.GroupWith("From B", "Bob");
        api.Returns(new SyncResponse(Cursor: 3, [SyncTestData.GroupChange(incoming, 1)], [], [], []));

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(1, outcome.Pulled);
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal(incoming.Id, record.State.Id);
        Assert.Equal(account, record.AccountScope);
        Assert.False(record.Dirty);
    }

    [Fact]
    public async Task PulledTombstone_RemovesTheLocalCopy()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Doomed", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 4, account, Dirty: false));
        api.Returns(new SyncResponse(Cursor: 9, [], [new SyncTombstone(SyncKinds.Group, state.Id, 4)], [], []));

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(1, outcome.Deleted);
        Assert.Empty(await local.LoadGroupsAsync(default));
    }

    [Fact]
    public async Task Unclaimed_KeepsTheLocalCopy_ButDropsTheAccountScope()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Kept", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 4, account, Dirty: false, Mode: StorageMode.Account));
        api.Returns(new SyncResponse(Cursor: 9, [], [], [new SyncUnclaimed(state.Id)], []));

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(1, outcome.Unclaimed);
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Null(record.AccountScope);
        // Back to "On this device", or this device's auto-claim (Mode == Account) would undo the removal.
        Assert.Equal(StorageMode.OnThisDevice, record.Mode);
    }

    [Fact]
    public async Task Unclaim_CallsTheServerFirst_ThenDropsTheScope_KeepingTheLocalCopy()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Leaving", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 5, account, Dirty: true, Mode: StorageMode.Account));

        var outcome = await service.UnclaimAsync(account, state.Id, default);

        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.Equal(1, outcome.Unclaimed);
        Assert.Equal(state.Id, Assert.Single(api.Unclaimed));

        // The copy is kept — it is not a delete — but account-free again, so no cycle will push it.
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Null(record.AccountScope);
        Assert.False(record.Dirty);
        Assert.Equal(StorageMode.OnThisDevice, record.Mode); // the intent follows, so no auto-claim re-claims it
    }

    [Fact]
    public async Task Unclaim_WhenTheServerIsUnreachable_ChangesNothingLocally()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Stay put", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 5, account, Dirty: true));
        api.UnclaimFails(ApiFailure.Unreachable);

        var outcome = await service.UnclaimAsync(account, state.Id, default);

        // Removal requires a connection (FR-026): the scope is not dropped with no server confirmation.
        Assert.Equal(SyncStatus.Offline, outcome.Status);
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal(account, record.AccountScope);
        Assert.True(record.Dirty);
    }

    [Fact]
    public async Task Unclaim_IsIdempotent_WhenTheGroupIsAlreadyAccountFree()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        // An un-claim from another device already dropped the scope; the local copy was kept.
        var state = SyncTestData.GroupWith("Already free", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 5, AccountScope: null, Dirty: false));

        var outcome = await service.UnclaimAsync(account, state.Id, default);

        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.Equal(state.Id, Assert.Single(api.Unclaimed));
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Null(record.AccountScope);
    }

    [Fact]
    public async Task Claim_UploadsTheGroup_AndMarksItAccountScoped_KeepingTheLocalCopy()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Family", "Alice");
        await SeedAsync(local, new SyncedGroupRecord(state, Version: 3, AccountScope: null, Dirty: false));

        var outcome = await service.ClaimAsync(account, [state.Id], default);

        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.Equal(1, outcome.Claimed);
        Assert.Equal(state.Id, Assert.Single(api.Claimed));

        // The local copy is kept and now account-scoped; not dirty — the claim route uploaded this version.
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal(account, record.AccountScope);
        Assert.False(record.Dirty);
        Assert.Equal(3, record.Version);
    }

    [Fact]
    public async Task Claim_OnlyTouchesAccountFreeGroups_LeavingAlreadyScopedOnesUnchanged()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var free = SyncTestData.GroupWith("Local", "A");
        var alreadyScoped = SyncTestData.GroupWith("Mine", "B");
        await SeedAsync(
            local,
            new SyncedGroupRecord(free, 1, AccountScope: null, Dirty: false),
            new SyncedGroupRecord(alreadyScoped, 2, AccountScope: account, Dirty: false));

        var outcome = await service.ClaimAsync(account, [free.Id, alreadyScoped.Id], default);

        // Only the account-free group is uploaded; the already-scoped one is neither re-sent nor changed.
        Assert.Equal(1, outcome.Claimed);
        Assert.Equal(free.Id, Assert.Single(api.Claimed));
    }

    [Fact]
    public async Task Claim_StopsAtTheFirstFailure_ArrivedGroupsStayClaimed_TheRestStayLocal()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var first = SyncTestData.GroupWith("First", "A");
        var second = SyncTestData.GroupWith("Second", "B");
        await SeedAsync(
            local,
            new SyncedGroupRecord(first, 1, AccountScope: null, Dirty: false),
            new SyncedGroupRecord(second, 1, AccountScope: null, Dirty: false));

        // The first upload succeeds, the second cannot reach the server.
        api.ClaimSucceeds().ClaimFails(ApiFailure.Unreachable);

        var outcome = await service.ClaimAsync(account, [first.Id, second.Id], default);

        Assert.Equal(SyncStatus.Offline, outcome.Status);
        Assert.Equal(1, outcome.Claimed);
        Assert.Equal(2, outcome.Requested);

        var records = await local.LoadGroupsAsync(default);
        // The arrived group is account-scoped; the one that failed is still purely local — and both copies
        // are still on the device (FR-025).
        Assert.Equal(account, records.Single(r => r.State.Id == first.Id).AccountScope);
        Assert.Null(records.Single(r => r.State.Id == second.Id).AccountScope);
    }

    [Fact]
    public async Task Claim_OfNothing_IsANoOpSuccess()
    {
        var account = Guid.NewGuid();
        var (service, _, api) = NewService();

        var outcome = await service.ClaimAsync(account, [], default);

        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.Equal(0, outcome.Claimed);
        Assert.Empty(api.Claimed);
    }

    [Fact]
    public async Task Collisions_AreSurfacedInTheOutcomeCount()
    {
        var account = Guid.NewGuid();
        var (service, _, api) = NewService();
        api.Returns(new SyncResponse(Cursor: 2, [], [], [], [new SyncCollision(SyncKinds.Group, Guid.NewGuid())]));

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(1, outcome.Collisions);
    }

    [Fact]
    public async Task UnreachableServer_LeavesEveryDirtyFlagAndCursorUntouched()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var state = SyncTestData.GroupWith("Pending", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 2, account, Dirty: true));
        api.Fails(ApiFailure.Unreachable);

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(SyncStatus.Offline, outcome.Status);
        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.True(record.Dirty);
        Assert.Equal(0, (await local.LoadStateAsync(default)).CursorFor(account));
    }

    [Fact]
    public async Task PulledBody_OverwritesACleanLocalCopy()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var original = SyncTestData.GroupWith("Old name", "A");
        await SeedAsync(local, new SyncedGroupRecord(original, 2, account, Dirty: false));
        var updated = original with { Name = "New name" };
        api.Returns(new SyncResponse(Cursor: 5, [SyncTestData.GroupChange(updated, 5)], [], [], []));

        await service.SyncNowAsync(account, default);

        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal("New name", record.State.Name);
        Assert.Equal(5, record.Version);
    }

    /// <summary>
    /// The store is re-read after the round-trip: an edit the user makes while the request is in
    /// flight must not be clobbered by the body the server echoes back. The concurrent edit survives,
    /// stays dirty, and is what pushes next cycle.
    /// </summary>
    [Fact]
    public async Task ConcurrentLocalEditDuringTheCycle_IsPreserved_NotClobberedByThePulledEcho()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var v2 = SyncTestData.GroupWith("Family", "Alice");
        await SeedAsync(local, new SyncedGroupRecord(v2, 2, account, Dirty: true));

        // While the request is "in flight", the user renames the group again → version 3, dirty.
        api.DuringExchange = async () =>
        {
            var current = (await local.LoadGroupsAsync(default))[0];
            var edited = current with { State = current.State with { Name = "Renamed again" }, Version = 3, Dirty = true };
            await local.SaveGroupsAsync([edited], default);
        };
        // The server echoes the version-2 body it received.
        api.Returns(new SyncResponse(Cursor: 8, [SyncTestData.GroupChange(v2, 2)], [], [], []));

        await service.SyncNowAsync(account, default);

        var record = Assert.Single(await local.LoadGroupsAsync(default));
        Assert.Equal("Renamed again", record.State.Name);
        Assert.Equal(3, record.Version);
        Assert.True(record.Dirty);
    }

    [Fact]
    public async Task Cursor_IsTrackedPerAccount()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var (service, local, api) = NewService();
        api.Returns(new SyncResponse(Cursor: 10, [], [], [], []));
        api.Returns(new SyncResponse(Cursor: 20, [], [], [], []));

        await service.SyncNowAsync(accountA, default);
        await service.SyncNowAsync(accountB, default);

        var state = await local.LoadStateAsync(default);
        Assert.Equal(10, state.CursorFor(accountA));
        Assert.Equal(20, state.CursorFor(accountB));
    }

    [Fact]
    public async Task PushedTombstone_IsClearedAfterASuccessfulCycle()
    {
        var account = Guid.NewGuid();
        var (service, local, _) = NewService();
        var id = Guid.NewGuid();
        var seedState = await local.LoadStateAsync(default);
        await local.SaveStateAsync(
            seedState with { Tombstones = [new PendingTombstone(account, SyncKinds.Group, id, 3)] },
            default);

        await service.SyncNowAsync(account, default);

        Assert.Empty((await local.LoadStateAsync(default)).Tombstones);
    }

    [Fact]
    public async Task RepeatedNoOpCycle_ChangesNothingAndDuplicatesNothing()
    {
        var account = Guid.NewGuid();
        var (service, local, _) = NewService();
        var state = SyncTestData.GroupWith("Stable", "A");
        await SeedAsync(local, new SyncedGroupRecord(state, 2, account, Dirty: false));

        await service.SyncNowAsync(account, default);
        await service.SyncNowAsync(account, default);

        Assert.Single(await local.LoadGroupsAsync(default));
    }
}
