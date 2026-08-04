using AppSuite.Main.Service.Test.Sync.TestSupport;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync;

/// <summary>
/// The last-write-wins escalation hook, proven wired and observed — not a stub (FR-024, the M2 lesson).
/// A concurrent edit discarded in full must emit a collision event carrying the aggregate kind and id,
/// because that count is the only evidence the parked conflict UI is parked on.
/// </summary>
[Collection(TwoClientSyncApi.CollectionName)]
public sealed class SyncTelemetryTests(TwoClientSyncFactory factory)
{
    [Fact]
    public async Task AnLwwDiscard_EmitsACollisionEvent_WithAggregateKindAndId()
    {
        var account = await factory.RegisterAccountAsync();
        var deviceA = account.NewDevice();
        var deviceB = account.NewDevice();

        var groupId = await deviceA.CreateGroupAsync("Shared", "Ada");
        await deviceA.ClaimAsync(groupId);
        await deviceA.SyncAsync();
        await deviceB.SyncAsync();

        // Both edit from the same base version; B arrives second and its push discards A's in full.
        await deviceA.RenameGroupAsync(groupId, "Alpha");
        await deviceB.RenameGroupAsync(groupId, "Bravo");
        await deviceA.SyncAsync();
        var bravo = await deviceB.SyncAsync();

        // The client saw the collision in its outcome, and the server-side hook recorded it with the
        // kind and id (the sink is shared across the suite, so this filters to this test's own group).
        Assert.Equal(1, bravo.Collisions);
        Assert.Contains((SyncKinds.Group, groupId), factory.Telemetry.Collisions);
    }
}
