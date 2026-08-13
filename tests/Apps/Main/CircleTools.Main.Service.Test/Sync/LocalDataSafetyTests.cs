using CircleTools.Main.Service.Test.Sync.TestSupport;

namespace CircleTools.Main.Service.Test.Sync;

/// <summary>
/// The cheapest test for the most expensive bug (NFR-008): a local-storage group count taken around
/// each account action, asserting none of them removes local data. The only thing that ever drops a
/// local group is the user deleting it on purpose.
/// </summary>
/// <remarks>
/// This slice covers the actions the delta engine expresses today — the initial reconcile a sign-in
/// runs, claiming, and a subsequent cycle. The remaining account actions in the TP-004 row (un-claim,
/// sign-out and account deletion) ride routes that land with later slices; their count assertions are
/// recorded as open in TP-004 rather than asserted here against machinery that does not exist yet.
/// </remarks>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class LocalDataSafetyTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task NeitherTheInitialReconcileClaimNorASubsequentCycle_ChangesTheLocalGroupCount()
    {
        var account = await factory.RegisterAccountAsync();
        var device = account.NewDevice();

        // Three account-free groups made before signing in — the local-first baseline.
        await device.CreateGroupAsync("Family", "Ada");
        await device.CreateGroupAsync("Friends", "Bernd");
        await device.CreateGroupAsync("Work", "Cora");
        Assert.Equal(3, await device.LocalGroupCountAsync());

        // Signing in runs a first reconcile. Pulling nothing new must not touch the local copies.
        await device.SyncAsync();
        Assert.Equal(3, await device.LocalGroupCountAsync());

        // Claiming scopes the groups to the account; it uploads, it never removes.
        await device.ClaimAsync();
        Assert.Equal(3, await device.LocalGroupCountAsync());

        await device.SyncAsync();
        Assert.Equal(3, await device.LocalGroupCountAsync());

        // A further cycle (the "on reconnect" trigger) leaves the count untouched.
        await device.SyncAsync();
        Assert.Equal(3, await device.LocalGroupCountAsync());
    }

    [Fact]
    public async Task AGroupClaimedOnAnotherDevice_ArrivingHere_AddsToButNeverReplacesTheLocalGroups()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        // B has its own account-free group; A claims a different one and syncs it up.
        await deviceB.CreateGroupAsync("B's own", "Bernd");
        Assert.Equal(1, await deviceB.LocalGroupCountAsync());

        var shared = await deviceA.CreateGroupAsync("Shared", "Ada");
        await deviceA.ClaimAsync(shared);
        await deviceA.SyncAsync();

        // When B pulls the shared group, it gains one — and keeps the one it already had.
        await deviceB.SyncAsync();
        Assert.Equal(2, await deviceB.LocalGroupCountAsync());
        Assert.NotNull(await deviceB.GroupAsync(shared));
    }
}
