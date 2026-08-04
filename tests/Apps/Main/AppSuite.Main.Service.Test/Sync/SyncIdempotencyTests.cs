using AppSuite.Main.Service.Test.Sync.TestSupport;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync;

/// <summary>
/// Replay safety (NFR-008): an interrupted cycle re-sends an already-applied push, and that must
/// change nothing and duplicate nothing. The interruption is real — the server applies the push and
/// the client never learns it did (<see cref="InterruptingSyncApi"/>) — so the re-send is the genuine
/// case, not a contrived double call.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class SyncIdempotencyTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task AlreadyAppliedPush_ReplayedAfterAnInterruption_ChangesNothingAndDuplicatesNothing()
    {
        var account = await factory.RegisterAccountAsync();
        var flaky = account.NewDevice(api => new InterruptingSyncApi(api));

        var groupId = await flaky.CreateGroupAsync("Family", "Ada", "Bernd");
        await flaky.ClaimAsync(groupId);

        // First cycle: the server applies the push, but the response is swallowed, so the copy stays
        // dirty and the cursor unmoved.
        var interrupted = await flaky.SyncAsync();
        Assert.NotEqual(SyncStatus.Synced, interrupted.Status);
        Assert.True((await flaky.RecordAsync(groupId))!.Dirty);

        // Second cycle: the same version is re-sent. It must settle cleanly and not duplicate the group.
        var replay = await flaky.SyncAsync();
        Assert.Equal(SyncStatus.Synced, replay.Status);
        Assert.False((await flaky.RecordAsync(groupId))!.Dirty);

        // A fresh reader sees exactly one group at the single clean version — no duplicate, no change.
        var reader = account.NewDevice();
        await reader.SyncAsync();
        var pulled = Assert.Single(await reader.RecordsAsync());
        Assert.Equal(groupId, pulled.State.Id);
        Assert.Equal("Family", pulled.State.Name);
        Assert.Equal(1, pulled.Version);
    }

    [Fact]
    public async Task AReplayedSequenceOfPushes_LeavesServerStateIdenticalToASingleCleanPush()
    {
        // A clean account: one push, no interruption.
        var clean = await factory.RegisterAccountAsync();
        var cleanDevice = clean.NewDevice();
        var cleanId = await cleanDevice.CreateGroupAsync("Reference", "Ada", "Bernd");
        await cleanDevice.ClaimAsync(cleanId);
        await cleanDevice.SyncAsync();
        var cleanState = await SoleGroupSeenByFreshReaderAsync(clean);

        // A replayed account: the same push, interrupted then re-sent.
        var replayed = await factory.RegisterAccountAsync();
        var flaky = replayed.NewDevice(api => new InterruptingSyncApi(api));
        var replayId = await flaky.CreateGroupAsync("Reference", "Ada", "Bernd");
        await flaky.ClaimAsync(replayId);
        await flaky.SyncAsync(); // interrupted
        await flaky.SyncAsync(); // replay
        var replayedState = await SoleGroupSeenByFreshReaderAsync(replayed);

        // The server state a fresh reader observes is identical in body and version (the id differs
        // because each account minted its own group; everything a reader converges on matches).
        Assert.Equal(cleanState.State.Name, replayedState.State.Name);
        Assert.Equal(cleanState.State.Members.Count, replayedState.State.Members.Count);
        Assert.Equal(cleanState.Version, replayedState.Version);
    }

    private static async Task<SyncedGroupRecord> SoleGroupSeenByFreshReaderAsync(SyncTestAccount account)
    {
        var reader = account.NewDevice();
        await reader.SyncAsync();
        return Assert.Single(await reader.RecordsAsync());
    }
}
