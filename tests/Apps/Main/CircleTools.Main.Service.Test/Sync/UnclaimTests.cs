using CircleTools.Main.Service.Test.Sync.TestSupport;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.Main.Service.Test.Sync;

/// <summary>
/// The FR-026 un-claim slice against the real API and Postgres: removing a group from the account
/// deletes the <em>server</em> copy (its assignments with it, ADR-0020) and keeps every local copy,
/// distinct from a delete. The properties that would fail silently — a kept local copy that was
/// actually lost, or a server copy that outlived the removal — are asserted directly.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class UnclaimTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task RemoveFromAccount_LeavesNoServerCopy_KeepsTheLocalCopyUsable_AndStopsSyncing()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();

        var groupId = await deviceA.CreateGroupAsync("Family", "Ada", "Bernd");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();

        var removal = await deviceA.UnclaimAsync(groupId);
        Assert.Equal(SyncStatus.Synced, removal.Status);

        // The local copy is kept and account-free — still fully usable, and no cycle will push it.
        var record = await deviceA.RecordAsync(groupId);
        Assert.NotNull(record);
        Assert.Null(record!.AccountScope);
        Assert.NotNull(await deviceA.GroupAsync(groupId));

        // A device that never saw the group pulls no body for it: the server holds no copy to hand out.
        var deviceC = account.NewDevice();
        await deviceC.SyncAsync();
        Assert.Null(await deviceC.GroupAsync(groupId));

        // A further edit on A pushes nothing about the group, because it has left the account.
        await deviceA.RenameGroupAsync(groupId, "Renamed while account-free");
        await deviceC.SyncAsync();
        Assert.Null(await deviceC.GroupAsync(groupId));
    }

    [Fact]
    public async Task RemovingADrawnGroup_DeletesTheServerSideAssignments()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();

        // A group carrying a completed draw — its assignments are in the body the server stores.
        var groupId = await deviceA.CreateGroupWithDrawAsync("Office");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();

        await deviceA.UnclaimAsync(groupId);

        // Deleting the server copy took its in-body assignments with it: a fresh device pulls nothing,
        // so the draw is not left on the server after the group has left the account (ADR-0020).
        var deviceC = account.NewDevice();
        await deviceC.SyncAsync();
        Assert.Null(await deviceC.GroupAsync(groupId));
    }

    [Fact]
    public async Task RemovingAGroupAlreadyRemovedElsewhere_ClearsTheLocalScope_AndSurfacesNoError()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        var groupId = await deviceA.CreateGroupAsync("Shared", "Ada");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();
        await deviceB.SyncAsync();

        // A removes it; B has not yet cycled, so B still holds it account-scoped.
        await deviceA.UnclaimAsync(groupId);

        // B removes the same group directly (not via a pulled marker). It is already gone on the server,
        // so this is idempotent: no error, the local scope is cleared, and the local copy is kept.
        var onB = await deviceB.UnclaimAsync(groupId);
        Assert.Equal(SyncStatus.Synced, onB.Status);
        var record = await deviceB.RecordAsync(groupId);
        Assert.NotNull(record);
        Assert.Null(record!.AccountScope);
    }
}
