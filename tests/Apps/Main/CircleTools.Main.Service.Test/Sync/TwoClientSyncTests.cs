// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Main.Service.Test.Sync.TestSupport;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.Main.Service.Test.Sync;

/// <summary>
/// The account sync between two devices of one account (FEAT-004, ADR-0041), with the real client cycle against
/// the real host: an edit converges, a concurrent edit is resolved by last-write-wins and counted, a lost
/// response is replayed without duplicating anything, and a refused local write never advances the cursor.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class TwoClientSyncTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task APinOnA_IsHeldByB_AfterItsNextSync()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();
        var group = Guid.NewGuid();

        await deviceA.PinAsync(group);
        var pushed = await deviceA.SyncAsync();
        var pulled = await deviceB.SyncAsync();

        Assert.Equal(1, pushed.Pushed);
        Assert.Equal(1, pulled.Pulled);
        Assert.Equal([group], await deviceB.PinnedAsync());
        Assert.False((await deviceA.PreferencesAsync())!.Dirty);
    }

    [Fact]
    public async Task ConcurrentEdits_TheLaterArrivalWinsInFull_AndTheDiscardIsCounted()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();
        var alpha = Guid.NewGuid();
        var bravo = Guid.NewGuid();

        // Both edit from nothing; B arrives second and its push discards A's in full.
        await deviceA.PinAsync(alpha);
        await deviceB.PinAsync(bravo);
        await deviceA.SyncAsync();
        var second = await deviceB.SyncAsync();
        await deviceA.SyncAsync();

        Assert.Equal(1, second.Collisions);
        Assert.Contains((SyncKinds.Preferences, account.AccountId), factory.Telemetry.Collisions);
        Assert.Equal([bravo], await deviceA.PinnedAsync());
        Assert.Equal([bravo], await deviceB.PinnedAsync());
    }

    [Fact]
    public async Task APushWhoseResponseWasLost_IsReplayed_AndSettlesAtTheSameVersion()
    {
        var account = await factory.RegisterAccountAsync();
        var device = account.NewDevice(api => new InterruptingSyncApi(api));
        var reader = account.NewDevice();
        var group = Guid.NewGuid();

        await device.PinAsync(group);
        var interrupted = await device.SyncAsync();
        var replayed = await device.SyncAsync();
        await reader.SyncAsync();

        Assert.Equal(SyncStatus.Offline, interrupted.Status);
        Assert.True(replayed.IsSuccess);
        Assert.False((await device.PreferencesAsync())!.Dirty);
        Assert.Equal([group], await reader.PinnedAsync());
        Assert.Equal(1, (await reader.PreferencesAsync())!.Version);
    }

    [Fact]
    public async Task ARefusedLocalWrite_LeavesTheCursorWhereItWas_SoTheNextCycleFetchesTheDeltaAgain()
    {
        var account = await factory.RegisterAccountAsync();
        var author = account.NewDevice();
        var reader = account.NewDevice();
        await author.PinAsync(Guid.NewGuid());
        await author.SyncAsync();

        reader.StorageRefusesWrites = true;
        var refused = await reader.SyncAsync();
        reader.StorageRefusesWrites = false;
        var retried = await reader.SyncAsync();

        Assert.Equal(SyncStatus.StorageUnavailable, refused.Status);
        Assert.Equal(1, retried.Pulled);
        Assert.True(await reader.CursorAsync() > 0);
    }
}
