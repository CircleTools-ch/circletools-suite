// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Main.Service.Test.Sync.TestSupport;

namespace CircleTools.Main.Service.Test.Sync;

/// <summary>
/// The cost of a quiet cycle (NFR-008): a device with nothing dirty and a current cursor exchanges no
/// aggregate bodies in either direction. Asserted on the wire, not on the client's outcome counts,
/// because a body pulled and then ignored would not show in a count.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class SyncPayloadTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task NoChangeCycle_TransfersNoAggregateBodies_EitherDirection()
    {
        var account = await factory.RegisterAccountAsync();

        // One device pins a group and syncs its preferences up.
        var author = account.NewDevice();
        await author.PinAsync(Guid.NewGuid());
        await author.SyncAsync();

        // A reader catches up, so its cursor is current and it has nothing pending.
        RecordingSyncApi? recorder = null;
        var reader = account.NewDevice(api => recorder = new RecordingSyncApi(api));
        await reader.SyncAsync(); // first cycle pulls the preferences and advances the cursor

        // The next cycle is the no-op: nothing dirty, cursor current.
        await reader.SyncAsync();

        Assert.NotNull(recorder!.LastRequest);
        Assert.NotNull(recorder.LastResponse);
        Assert.Empty(recorder.LastRequest!.Changes);
        Assert.Empty(recorder.LastRequest.Tombstones);
        Assert.Empty(recorder.LastResponse!.Changes);
        Assert.Empty(recorder.LastResponse.Tombstones);
    }
}
