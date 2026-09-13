using System.Text.Json;
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
    private static SharedSyncService Service(LocalSyncStore store, FakeSharedGroupApi api) => new(store, api);

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
