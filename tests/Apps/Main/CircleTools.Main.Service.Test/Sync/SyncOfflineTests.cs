using CircleTools.Main.Service.Test.Sync.TestSupport;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.Main.Service.Test.Sync;

/// <summary>
/// Offline editing and reconciliation (FR-024 / NFR-001/008): a write takes full effect with no
/// awaited network call and is reconciled later, and all pending work drains in a single cycle when
/// connectivity returns.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class SyncOfflineTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task OfflineWithNoCycle_CreateAddMembersAndDraw_AllPersistLocally_AndSendNothing()
    {
        var account = await factory.RegisterAccountAsync();
        var device = account.NewDevice();

        // A whole flow with no sync call at all: create, populate with members and wishlists, draw.
        // Local-first, zero network.
        var groupId = await device.CreateGroupWithDrawAsync("Offline party");

        var group = await device.GroupAsync(groupId);
        Assert.NotNull(group);
        Assert.Equal(4, group!.Members.Count);
        var occasion = Assert.Single(group.Events);
        Assert.Contains(group.Members, m => occasion.WishlistFor(m.Id).Items.Count > 0);
        Assert.Single(occasion.Draws);

        // Nothing was claimed, so nothing is account-scoped: an unsent, account-free local group.
        var record = (await device.RecordAsync(groupId))!;
        Assert.Null(record.AccountScope);
        Assert.True(record.Dirty);
    }

    [Fact]
    public async Task AfterBeingOfflineWithPendingChanges_AllPendingAggregatesArePushedWithinOneCycle()
    {
        var account = await factory.RegisterAccountAsync();
        var author = account.NewDevice();

        // Several claimed groups queued up while offline (no cycle between them).
        for (var i = 0; i < 4; i++)
        {
            await author.CreateGroupAsync($"Group {i}", "Ada");
        }

        // Stage the claims locally (no upload), so the cycle is what must drain every pending aggregate.
        await author.StageClaimAsync();
        Assert.All(await author.RecordsAsync(), r => Assert.True(r.Dirty));

        // One cycle drains every pending aggregate.
        var outcome = await author.SyncAsync();
        Assert.Equal(SyncStatus.Synced, outcome.Status);
        Assert.Equal(4, outcome.Pushed);
        Assert.All(await author.RecordsAsync(), r => Assert.False(r.Dirty));

        // And all four are on the account: a second device pulls the complete set in its first cycle.
        var reader = account.NewDevice();
        await reader.SyncAsync();
        Assert.Equal(4, await reader.LocalGroupCountAsync());
    }
}
