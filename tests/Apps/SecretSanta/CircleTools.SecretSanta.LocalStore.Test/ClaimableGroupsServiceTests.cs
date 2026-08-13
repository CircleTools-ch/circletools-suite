using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="ClaimableGroupsService"/> (FEAT-004): the read-model the claim / un-claim /
/// sync-status screens render. The account scope lives only in the LocalStore record, so this is what
/// tells the UI which groups are still account-free and which carry a draw whose assignments a claim
/// would upload (NFR-002).
/// </summary>
public sealed class ClaimableGroupsServiceTests
{
    private static (ClaimableGroupsService Service, LocalSyncStore Local) NewService()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        return (new ClaimableGroupsService(local), local);
    }

    private static DrawState ADraw() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, []);

    [Fact]
    public async Task GetAccountFree_ReturnsOnlyGroupsNotClaimedIntoAnyAccount()
    {
        var (service, local) = NewService();
        var free = SyncTestData.GroupWith("Local only", "Alice");
        var scoped = SyncTestData.GroupWith("On account", "Bob");
        await local.SaveGroupsAsync(
        [
            new SyncedGroupRecord(free, Version: 1, AccountScope: null, Dirty: false),
            new SyncedGroupRecord(scoped, Version: 1, AccountScope: Guid.NewGuid(), Dirty: false),
        ]);

        var result = await service.GetAccountFreeAsync();

        var only = Assert.Single(result);
        Assert.Equal(free.Id, only.Id);
        Assert.True(only.IsAccountFree);
    }

    [Fact]
    public async Task MemberCount_IsReported_IncludingExactlyOne()
    {
        var (service, local) = NewService();
        var solo = SyncTestData.GroupWith("Solo", "Alice");
        await local.SaveGroupsAsync([new SyncedGroupRecord(solo, 1, null, false)]);

        var group = Assert.Single(await service.GetAccountFreeAsync());

        Assert.Equal(1, group.MemberCount);
    }

    [Fact]
    public async Task HasCurrentDraw_IsTrue_ForADrawnGroup()
    {
        var (service, local) = NewService();
        var drawn = SyncTestData.GroupWith("Drawn", "Alice", "Bob") with { Draws = [ADraw()] };
        await local.SaveGroupsAsync([new SyncedGroupRecord(drawn, 1, null, false)]);

        var group = Assert.Single(await service.GetAccountFreeAsync());

        Assert.True(group.HasCurrentDraw);
    }

    [Fact]
    public async Task HasCurrentDraw_IsFalse_WhenTheDrawWasInvalidatedByAMembershipChange()
    {
        var (service, local) = NewService();
        var invalidated = SyncTestData.GroupWith("Stale", "Alice", "Bob")
            with { Draws = [ADraw()], DrawInvalidatedByMembershipChange = true };
        await local.SaveGroupsAsync([new SyncedGroupRecord(invalidated, 1, null, false)]);

        var group = Assert.Single(await service.GetAccountFreeAsync());

        Assert.False(group.HasCurrentDraw);
    }

    [Fact]
    public async Task GetAll_FlagsEachGroupAccountFreeOrNot()
    {
        var (service, local) = NewService();
        var account = Guid.NewGuid();
        await local.SaveGroupsAsync(
        [
            new SyncedGroupRecord(SyncTestData.GroupWith("Free"), 1, null, false),
            new SyncedGroupRecord(SyncTestData.GroupWith("Scoped"), 1, account, false),
        ]);

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Single(all, g => g.IsAccountFree);
        Assert.Single(all, g => !g.IsAccountFree);
    }
}
