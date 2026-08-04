using System.Text.Json;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Repository.Test;

/// <summary>
/// Behaviour of <see cref="SyncStore"/> against real Postgres (TP-004). The two silent-failure
/// properties get the weight: convergence (a change reaches another device; a delete does not
/// resurrect) and data safety (last-write-wins is bounded and <em>counted</em>). The group body is
/// asserted to survive as an opaque document — the store never needs its schema.
/// </summary>
[Collection(SharedSyncDatabase.CollectionName)]
public sealed class SyncStoreTests(SyncPostgresFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AClaimedGroupBody_RoundTripsAsAnOpaqueDocument_AboveALowerCursor()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var response = await ExchangeAsync(
            account,
            new SyncRequest(
                "device-a",
                Cursor: 0,
                Changes: [new SyncChange("group", groupId, 1, Body("""{ "name": "Family", "members": [{ "name": "Anna" }] }"""))],
                Tombstones: []));

        Assert.Equal(1, response.Cursor);
        var change = Assert.Single(response.Changes);
        Assert.Equal(groupId, change.Id);
        Assert.Equal("group", change.Kind);
        Assert.Equal(1, change.Version);

        // The nested body survived a jsonb round-trip without the store ever binding the schema.
        Assert.Equal("Family", change.Body.GetProperty("name").GetString());
        Assert.Equal("Anna", change.Body.GetProperty("members")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AChangeFromOneDevice_IsSeenByAnother_AfterItsNextCycle()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "n": 1 }""")));

        // Device B, new to this account, pushes nothing and pulls A's change.
        var onB = await ExchangeAsync(account, new SyncRequest("B", 0, [], []));

        var seen = Assert.Single(onB.Changes);
        Assert.Equal(groupId, seen.Id);
    }

    [Fact]
    public async Task NoChangeCycle_TransfersNoAggregateBodies_EitherDirection()
    {
        var account = Guid.NewGuid();

        // Nothing pushed, cursor current: the request lists no changes and the response returns none.
        var response = await ExchangeAsync(account, new SyncRequest("A", 0, [], []));

        Assert.Empty(response.Changes);
        Assert.Empty(response.Tombstones);
        Assert.Empty(response.Unclaimed);
        Assert.Equal(0, response.Cursor);
    }

    [Fact]
    public async Task AnLwwDiscard_EmitsACollisionEvent_AndTheLaterServerTimeSurvivesInFull()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var clock = new MutableClock(Base);
        var telemetry = new CapturingSyncTelemetry();

        // A and B both edited the same group offline from a common ancestor, so both push version 1.
        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "who": "A" }""")), telemetry, clock);

        clock.Advance(TimeSpan.FromSeconds(5));
        var onB = await ExchangeAsync(account, Push("B", 0, Change(groupId, 1, """{ "who": "B" }""")), telemetry, clock);

        // B arrived later by the server clock, so B survives in full and A is discarded in full.
        var collision = Assert.Single(onB.Collisions);
        Assert.Equal(groupId, collision.Id);
        Assert.Equal("group", collision.Kind);

        var recorded = Assert.Single(telemetry.Collisions);
        Assert.Equal(groupId, recorded.AggregateId);
        Assert.Equal("group", recorded.Kind);

        var onC = await ExchangeAsync(account, new SyncRequest("C", 0, [], []));
        var survivor = onC.Changes.Single(c => c.Id == groupId);
        Assert.Equal("B", survivor.Body.GetProperty("who").GetString());
    }

    [Fact]
    public async Task ACleanFastForward_IsNotACollision()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var telemetry = new CapturingSyncTelemetry();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "v": 1 }""")), telemetry);
        // The next edit was made from the stored version, so it is version 2 — a clean update.
        var second = await ExchangeAsync(account, Push("A", 1, Change(groupId, 2, """{ "v": 2 }""")), telemetry);

        Assert.Empty(second.Collisions);
        Assert.Empty(telemetry.Collisions);
    }

    [Fact]
    public async Task ADeletedGroup_IsNotResurrectedByAStalePush()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, "{}")));
        await ExchangeAsync(account, new SyncRequest("A", 1, [], [new SyncTombstone("group", groupId, 2)]));

        // A device that never saw the delete pushes a stale edit at or below the tombstone's version.
        await ExchangeAsync(account, Push("B", 0, Change(groupId, 2, """{ "resurrected": true }""")));

        var onD = await ExchangeAsync(account, new SyncRequest("D", 0, [], []));
        Assert.DoesNotContain(onD.Changes, c => c.Id == groupId);
        Assert.Contains(onD.Tombstones, t => t.Id == groupId);
    }

    [Fact]
    public async Task Unclaiming_DeletesTheServerCopy_AndRecordsAnUnclaimMarkerNotATombstone()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "assignments": [1, 2] }""")));
        await UnclaimAsync(account, groupId);

        // A device that has never synced pulls the un-claim marker and no group body — the server copy
        // (its in-body assignments with it) is gone, but the marker keeps the local copy on other devices.
        var onB = await ExchangeAsync(account, new SyncRequest("B", 0, [], []));
        Assert.DoesNotContain(onB.Changes, c => c.Id == groupId);
        Assert.DoesNotContain(onB.Tombstones, t => t.Id == groupId);
        Assert.Contains(onB.Unclaimed, u => u.Id == groupId);
    }

    [Fact]
    public async Task Unclaiming_IsIdempotent_AndSurfacesNoError()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, "{}")));
        await UnclaimAsync(account, groupId);
        // Un-claiming a group already removed elsewhere throws nothing and still leaves exactly one marker.
        await UnclaimAsync(account, groupId);

        await using var provider = fixture.CreateProvider();
        var context = provider.GetRequiredService<SyncDbContext>();
        var markers = await context.Markers
            .Where(m => m.AccountId == account && m.AggregateId == groupId)
            .ToListAsync();
        var marker = Assert.Single(markers);
        Assert.Equal(SyncMarkerType.Unclaim, marker.MarkerType);
    }

    [Fact]
    public async Task AStandingTombstone_IsNotDowngradedByAnUnclaim()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, "{}")));
        await ExchangeAsync(account, new SyncRequest("A", 1, [], [new SyncTombstone("group", groupId, 2)]));

        // A racing un-claim of the same group must not turn the delete into a keep-the-copy marker: the
        // delete removes the group everywhere and would otherwise resurrect on a device that never saw it.
        await UnclaimAsync(account, groupId);

        var onB = await ExchangeAsync(account, new SyncRequest("B", 0, [], []));
        Assert.Contains(onB.Tombstones, t => t.Id == groupId);
        Assert.DoesNotContain(onB.Unclaimed, u => u.Id == groupId);
    }

    [Fact]
    public async Task AReclaimAfterAnUnclaim_RecreatesTheAggregate_AndClearsTheStaleMarker()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "v": 1 }""")));
        await UnclaimAsync(account, groupId);

        // Re-claiming pushes the body again; the un-claim marker (not a tombstone) does not block it.
        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, """{ "v": 1 }""")));

        var onC = await ExchangeAsync(account, new SyncRequest("C", 0, [], []));
        Assert.Contains(onC.Changes, c => c.Id == groupId);
        Assert.DoesNotContain(onC.Unclaimed, u => u.Id == groupId);
    }

    [Fact]
    public async Task ServerSequence_IsMonotonicPerAccount_AcrossConcurrentApplies()
    {
        var account = Guid.NewGuid();
        const int concurrency = 8;

        var applies = Enumerable.Range(0, concurrency).Select(i =>
            ExchangeAsync(account, Push($"device-{i}", 0, Change(Guid.NewGuid(), 1, "{}"))));

        await Task.WhenAll(applies);

        await using var provider = fixture.CreateProvider();
        var context = provider.GetRequiredService<SyncDbContext>();
        var sequences = await context.Aggregates
            .Where(a => a.AccountId == account)
            .Select(a => a.ServerSeq)
            .ToListAsync();

        Assert.Equal(concurrency, sequences.Count);
        Assert.Equal(concurrency, sequences.Distinct().Count());
    }

    [Fact]
    public async Task AMarkerIsPruned_OnlyOnceEveryDeviceHasAdvancedPastIt()
    {
        var account = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Device B registers before the delete exists, and does not sync again for a while.
        await ExchangeAsync(account, new SyncRequest("B", 0, [], []));

        // Device A creates then deletes the group, minting a tombstone marker A has now seen.
        await ExchangeAsync(account, Push("A", 0, Change(groupId, 1, "{}")));
        await ExchangeAsync(account, new SyncRequest("A", 1, [], [new SyncTombstone("group", groupId, 2)]));

        var staleBefore = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);

        // B is still behind the marker, so it must not be pruned.
        var prunedWhileBBehind = await PruneAsync(account, staleBefore);
        Assert.Equal(0, prunedWhileBBehind);
        Assert.True(await MarkerExistsAsync(account, groupId));

        // Once B has cycled and caught up, every device has seen the marker and it can be pruned.
        await ExchangeAsync(account, new SyncRequest("B", 0, [], []));
        var prunedAfterBCaughtUp = await PruneAsync(account, staleBefore);
        Assert.Equal(1, prunedAfterBCaughtUp);
        Assert.False(await MarkerExistsAsync(account, groupId));
    }

    private async Task<SyncResponse> ExchangeAsync(
        Guid accountId,
        SyncRequest request,
        ISyncTelemetry? telemetry = null,
        TimeProvider? clock = null)
    {
        await using var provider = fixture.CreateProvider();
        var store = new SyncStore(
            provider.GetRequiredService<SyncDbContext>(),
            telemetry ?? new CapturingSyncTelemetry(),
            clock ?? TimeProvider.System);

        return await store.ExchangeAsync(accountId, request);
    }

    private async Task UnclaimAsync(Guid accountId, Guid groupId)
    {
        await using var provider = fixture.CreateProvider();
        var store = new SyncStore(
            provider.GetRequiredService<SyncDbContext>(),
            new CapturingSyncTelemetry(),
            TimeProvider.System);

        await store.UnclaimAsync(accountId, groupId);
    }

    private async Task<int> PruneAsync(Guid accountId, DateTimeOffset staleBefore)
    {
        await using var provider = fixture.CreateProvider();
        var store = new SyncStore(
            provider.GetRequiredService<SyncDbContext>(),
            new CapturingSyncTelemetry(),
            TimeProvider.System);

        return await store.PruneMarkersAsync(accountId, staleBefore);
    }

    private async Task<bool> MarkerExistsAsync(Guid accountId, Guid aggregateId)
    {
        await using var provider = fixture.CreateProvider();
        var context = provider.GetRequiredService<SyncDbContext>();

        return await context.Markers.AnyAsync(m => m.AccountId == accountId && m.AggregateId == aggregateId);
    }

    private static SyncRequest Push(string deviceId, long cursor, SyncChange change) =>
        new(deviceId, cursor, [change], []);

    private static SyncChange Change(Guid id, long version, string bodyJson) =>
        new("group", id, version, Body(bodyJson));

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>A capturing <see cref="ISyncTelemetry"/> that records collisions for assertion.</summary>
internal sealed class CapturingSyncTelemetry : ISyncTelemetry
{
    private readonly List<(string Kind, Guid AggregateId)> _collisions = [];

    /// <summary>The collisions recorded so far, in order.</summary>
    public IReadOnlyList<(string Kind, Guid AggregateId)> Collisions => _collisions;

    /// <inheritdoc />
    public void RecordCollision(string kind, Guid aggregateId) => _collisions.Add((kind, aggregateId));
}

/// <summary>A hand-set <see cref="TimeProvider"/> so the server clock is an input, not a wall clock.</summary>
internal sealed class MutableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far to advance.</param>
    public void Advance(TimeSpan by) => _now += by;
}
