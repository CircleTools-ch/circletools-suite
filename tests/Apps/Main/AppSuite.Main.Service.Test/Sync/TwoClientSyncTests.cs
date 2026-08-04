using AppSuite.Main.Service.Test.Sync.TestSupport;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync;

/// <summary>
/// The spine of TP-004: two independent client stores driving real sync cycles against the real API
/// and Postgres, asserting the two properties that fail silently — convergence and data safety
/// (NFR-008). Every test here has device B observe something device A did; a single change tracker
/// could not express any of it.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class TwoClientSyncTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task ClaimedGroupOnA_AppearsOnB_WithMembersWishlistsExclusionsAndDraw()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        var groupId = await deviceA.CreateGroupWithDrawAsync("Family");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();

        await deviceB.SyncAsync();

        var onA = (await deviceA.GroupAsync(groupId))!;
        var onB = await deviceB.GroupAsync(groupId);
        Assert.NotNull(onB);

        // The whole aggregate crossed, not just the name: members with their wishlists, the exclusion,
        // and the completed draw's assignments (FR-023).
        Assert.Equal(onA.Name, onB.Name);
        Assert.Equal(
            onA.Members.Select(m => (m.Name, string.Join(",", m.Wishlist.Items))).OrderBy(x => x.Name),
            onB.Members.Select(m => (m.Name, string.Join(",", m.Wishlist.Items))).OrderBy(x => x.Name));
        Assert.Equal(onA.Exclusions.Count, onB.Exclusions.Count);
        Assert.Equal(
            onA.Draws.Single().Assignments.Select(a => (a.GiverMemberId, a.RecipientMemberId)).OrderBy(x => x.GiverMemberId),
            onB.Draws.Single().Assignments.Select(a => (a.GiverMemberId, a.RecipientMemberId)).OrderBy(x => x.GiverMemberId));
    }

    [Fact]
    public async Task EditOnA_IsHeldByB_AfterItsNextSync()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        await deviceA.RenameGroupAsync(groupId, "Renamed on A");
        await deviceA.SyncAsync();

        // B still holds the old name until it cycles — then it converges.
        await deviceB.SyncAsync();

        Assert.Equal("Renamed on A", (await deviceB.GroupAsync(groupId))!.Name);
    }

    [Fact]
    public async Task GroupDeletedOnA_IsRemovedOnB_AndDoesNotReappear()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        await deviceA.DeleteGroupAsync(groupId);
        await deviceA.SyncAsync();

        await deviceB.SyncAsync();
        Assert.Null(await deviceB.GroupAsync(groupId));

        // A later, quiet cycle on either device must not resurrect it.
        await deviceB.SyncAsync();
        await deviceA.SyncAsync();
        Assert.Null(await deviceB.GroupAsync(groupId));
        Assert.Null(await deviceA.GroupAsync(groupId));
    }

    [Fact]
    public async Task AfterOneCycleWithNoFurtherEdits_BothDevicesHoldTheSameAggregatesAtTheSameVersions()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        var first = await deviceA.CreateGroupAsync("Friends", "Ada", "Bernd");
        var second = await deviceA.CreateGroupAsync("Work", "Cora");
        await deviceA.ClaimAsync();
        await deviceA.SyncAsync();

        // One cycle on B, then a settling cycle on A so a change A made is not still pending anywhere.
        await deviceB.SyncAsync();
        await deviceA.SyncAsync();

        var onA = (await deviceA.RecordsAsync()).OrderBy(r => r.State.Id).ToList();
        var onB = (await deviceB.RecordsAsync()).OrderBy(r => r.State.Id).ToList();

        Assert.Equal(2, onB.Count);
        Assert.Equal(new[] { first, second }.OrderBy(x => x), onB.Select(r => r.State.Id).OrderBy(x => x));
        Assert.Equal(
            onA.Select(r => (r.State.Id, r.Version)),
            onB.Select(r => (r.State.Id, r.Version)));
        Assert.All(onA, r => Assert.False(r.Dirty));
        Assert.All(onB, r => Assert.False(r.Dirty));
        Assert.Equal(await deviceA.CursorAsync(), await deviceB.CursorAsync());
    }

    [Fact]
    public async Task OnceEveryDeviceHasCycled_ADeletedGroupIsAbsentEverywhere_AndDoesNotReappearOnAnyLaterCycle()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        // B has seen the group. A deletes it and both devices cycle: the tombstone reaches B.
        await deviceA.DeleteGroupAsync(groupId);
        await deviceA.SyncAsync();
        await deviceB.SyncAsync();

        Assert.Null(await deviceA.GroupAsync(groupId));
        Assert.Null(await deviceB.GroupAsync(groupId));

        // Several further cycles, in both orders, must never bring it back.
        for (var i = 0; i < 3; i++)
        {
            await deviceA.SyncAsync();
            await deviceB.SyncAsync();
        }

        Assert.Empty(await deviceA.GroupsAsync());
        Assert.Empty(await deviceB.GroupsAsync());
    }

    [Fact]
    public async Task PendingChange_IsPushedWithoutTheUserAsking_WhenConnectivityReturns_AndStatusClears()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        // An edit made while "offline" — no cycle runs — stays dirty and local.
        await deviceA.RenameGroupAsync(groupId, "Edited offline");
        Assert.True((await deviceA.RecordAsync(groupId))!.Dirty);

        // Connectivity returns and the same cycle every trigger runs pushes it; the dirty flag clears.
        var outcome = await deviceA.SyncAsync();
        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.False((await deviceA.RecordAsync(groupId))!.Dirty);

        await deviceB.SyncAsync();
        Assert.Equal("Edited offline", (await deviceB.GroupAsync(groupId))!.Name);
    }

    [Fact]
    public async Task OfflineDelete_PushesATombstone_AndIsGoneOnOtherDevices()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        // Deleted while offline: no cycle yet. The next cycle carries the tombstone.
        await deviceA.DeleteGroupAsync(groupId);
        var outcome = await deviceA.SyncAsync();
        Assert.Equal(SyncStatus.Synced, outcome.Status);

        await deviceB.SyncAsync();
        Assert.Null(await deviceB.GroupAsync(groupId));
    }

    [Fact]
    public async Task SameGroupEditedOfflineOnBoth_LaterServerTimeSurvivesInFull_OtherDiscardedInFull()
    {
        var account = await factory.RegisterAccountAsync();
        var (deviceA, deviceB, groupId) = await ClaimedOnBothAsync(account);

        // Both edit the same group from the same base version while offline.
        await deviceA.RenameGroupAsync(groupId, "Alpha wins?");
        await deviceB.RenameGroupAsync(groupId, "Bravo wins?");

        // A reaches the server first; B second, so B is the later arrival and survives in full.
        await deviceA.SyncAsync();
        var bravo = await deviceB.SyncAsync();
        Assert.Equal(1, bravo.Collisions);

        // A pulls the winning body on its next cycle; both converge on exactly one survivor — B's.
        await deviceA.SyncAsync();
        Assert.Equal("Bravo wins?", (await deviceA.GroupAsync(groupId))!.Name);
        Assert.Equal("Bravo wins?", (await deviceB.GroupAsync(groupId))!.Name);
    }

    /// <summary>
    /// Sets up one claimed group visible on both devices: A creates and claims it, both cycle, and B
    /// holds it account-scoped and clean. The common starting point for the edit/delete/collision cases.
    /// </summary>
    private static async Task<(SyncClient A, SyncClient B, Guid GroupId)> ClaimedOnBothAsync(SyncTestAccount account)
    {
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        var groupId = await deviceA.CreateGroupAsync("Shared", "Ada", "Bernd");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();
        await deviceB.SyncAsync();

        return (deviceA, deviceB, groupId);
    }
}
