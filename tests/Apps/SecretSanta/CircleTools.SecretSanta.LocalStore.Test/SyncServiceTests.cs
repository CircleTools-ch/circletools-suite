using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for the <see cref="SyncService"/> cycle (FEAT-004, ADR-0041): what it pushes, how it applies the
/// server delta to the preferences document, and — the properties that fail silently — that it clears nothing
/// it should not and advances the cursor only on a completed cycle (NFR-008). Groups have their own sync
/// (ADR-0086).
/// </summary>
public sealed class SyncServiceTests
{
    private static (SyncService Service, LocalSyncStore Local, FakeSyncApi Api) NewService()
    {
        var local = new LocalSyncStore(new InMemoryKeyValueStore());
        var api = new FakeSyncApi();
        return (new SyncService(local, api), local, api);
    }

    private static Task SeedPreferencesAsync(LocalSyncStore local, Guid account, long version, bool dirty, params Guid[] pinned) =>
        local.SavePreferencesAsync(new SyncedPreferencesRecord(new PreferencesState(pinned), version, account, dirty), default);

    private static JsonElement Body(params Guid[] pinned) =>
        JsonSerializer.SerializeToElement(new PreferencesState(pinned), SyncJsonContext.Default.PreferencesState);

    [Fact]
    public async Task NoOpCycle_SendsNoBodiesOrTombstones()
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
    public async Task DirtyPreferences_ArePushedAsPreferencesKind_ThenMarkedSynced()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var group = Guid.NewGuid();
        await SeedPreferencesAsync(local, account, 1, dirty: true, group);
        api.Returns(new SyncResponse(Cursor: 3, [], [], []));

        var outcome = await service.SyncNowAsync(account, default);

        // The preferences document rides the cycle under its own kind, keyed by the account id.
        var change = Assert.Single(api.LastRequest!.Changes);
        Assert.Equal(SyncKinds.Preferences, change.Kind);
        Assert.Equal(account, change.Id);
        Assert.Equal(1, change.Version);
        Assert.Equal(1, outcome.Pushed);

        var stored = await local.LoadPreferencesAsync(default);
        Assert.False(stored!.Dirty);
        Assert.Equal([group], stored.State.PinnedGroupIds);
    }

    [Fact]
    public async Task PreferencesForAnotherAccount_AreNotPushed()
    {
        var (service, local, api) = NewService();
        await SeedPreferencesAsync(local, Guid.NewGuid(), 1, dirty: true, Guid.NewGuid());

        await service.SyncNowAsync(Guid.NewGuid(), default);

        Assert.Empty(api.LastRequest!.Changes);
    }

    [Fact]
    public async Task PulledPreferences_AreAppliedLocally()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var group = Guid.NewGuid();
        api.Returns(new SyncResponse(Cursor: 5, [new SyncChange(SyncKinds.Preferences, account, 4, Body(group))], [], []));

        var outcome = await service.SyncNowAsync(account, default);

        var stored = await local.LoadPreferencesAsync(default);
        Assert.False(stored!.Dirty);
        Assert.Equal(4, stored.Version);
        Assert.Equal([group], stored.State.PinnedGroupIds);
        Assert.Equal(1, outcome.Pulled);
    }

    [Fact]
    public async Task ALocalEditAtLeastAsNew_IsNotOverwrittenByThePulledBody()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        var mine = Guid.NewGuid();
        await SeedPreferencesAsync(local, account, 5, dirty: true, mine);

        // The server answers with an older body from another device; the push was not confirmed for version 5.
        api.Returns(new SyncResponse(Cursor: 7, [new SyncChange(SyncKinds.Preferences, account, 4, Body(Guid.NewGuid()))], [], []));
        api.DuringExchange = () => SeedPreferencesAsync(local, account, 6, dirty: true, mine);

        await service.SyncNowAsync(account, default);

        var stored = await local.LoadPreferencesAsync(default);
        Assert.True(stored!.Dirty);
        Assert.Equal(6, stored.Version);
        Assert.Equal([mine], stored.State.PinnedGroupIds);
    }

    [Fact]
    public async Task Collisions_AreSurfacedInTheOutcomeCount()
    {
        var (service, _, api) = NewService();
        api.Returns(new SyncResponse(Cursor: 2, [], [], [new SyncCollision(SyncKinds.Preferences, Guid.NewGuid())]));

        var outcome = await service.SyncNowAsync(Guid.NewGuid(), default);

        Assert.Equal(1, outcome.Collisions);
    }

    [Fact]
    public async Task UnreachableServer_LeavesTheDirtyFlagAndCursorUntouched()
    {
        var account = Guid.NewGuid();
        var (service, local, api) = NewService();
        await SeedPreferencesAsync(local, account, 2, dirty: true, Guid.NewGuid());
        api.Fails(ApiFailure.Unreachable);

        var outcome = await service.SyncNowAsync(account, default);

        Assert.Equal(SyncStatus.Offline, outcome.Status);
        Assert.True((await local.LoadPreferencesAsync(default))!.Dirty);
        Assert.Equal(0, (await local.LoadStateAsync(default)).CursorFor(account));
    }

    [Fact]
    public async Task Cursor_IsTrackedPerAccount()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var (service, local, api) = NewService();
        api.Returns(new SyncResponse(Cursor: 10, [], [], []));
        api.Returns(new SyncResponse(Cursor: 20, [], [], []));

        await service.SyncNowAsync(accountA, default);
        await service.SyncNowAsync(accountB, default);

        var state = await local.LoadStateAsync(default);
        Assert.Equal(10, state.CursorFor(accountA));
        Assert.Equal(20, state.CursorFor(accountB));
    }
}
