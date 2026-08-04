using System.Text.Json;
using AppSuite.ApiClient;
using AppSuite.Main.Contracts;
using AppSuite.SecretSanta.Core.Persistence;
using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// Runs one M3 sync cycle for a signed-in account (FEAT-004): push what is locally dirty, apply the
/// server delta above the cursor, reconcile, and advance the cursor — the single mechanism behind all
/// four triggers (app start, on change, on reconnect, manual retry), which are thin callers of
/// <see cref="SyncNowAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only claimed groups scoped to the calling account are pushed, and only to that account — a group
/// claimed to a different account that once used this device is never sent to the account signing in
/// now (FR-024). A no-op cycle (nothing dirty, cursor current) carries no aggregate bodies in either
/// direction (NFR-008).
/// </para>
/// <para>
/// <strong>Data safety.</strong> Nothing local is removed except by an applied tombstone (a group
/// deleted on the account) or dropped from account scope by an un-claim marker (the local copy is
/// kept). If the server is unreachable or a local write is refused, the cycle ends without advancing
/// the cursor, so the delta is simply re-fetched next time — an interrupted cycle loses nothing and
/// duplicates nothing (NFR-008).
/// </para>
/// <para>
/// The store is re-read after the network round-trip so an edit the user made while the request was in
/// flight is reconciled, not clobbered: a locally-dirty copy is never overwritten by a pulled body it
/// is at least as new as, and a push is only marked synced if the copy still holds the version that
/// was sent.
/// </para>
/// </remarks>
/// <param name="local">The shared persistence gateway.</param>
/// <param name="api">The delta-exchange port.</param>
public sealed class SyncService(ILocalSyncStore local, ISyncApi api)
{
    /// <summary>Runs one cycle for <paramref name="accountId"/>.</summary>
    /// <param name="accountId">The signed-in account — the only sync scope.</param>
    /// <param name="cancellationToken">Cancels the cycle.</param>
    /// <returns>How the cycle ended and what moved.</returns>
    public async Task<SyncOutcome> SyncNowAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var records = await local.LoadGroupsAsync(cancellationToken);
        var state = await local.LoadStateAsync(cancellationToken);

        var pushedChanges = records
            .Where(r => r.AccountScope == accountId && r.Dirty)
            .Select(r => new SyncChange(
                SyncKinds.Group,
                r.State.Id,
                r.Version,
                JsonSerializer.SerializeToElement(r.State, SyncJsonContext.Default.GroupState)))
            .ToList();

        var pushedTombstones = state.Tombstones
            .Where(t => t.AccountId == accountId)
            .Select(t => new SyncTombstone(t.Kind, t.Id, t.Version))
            .ToList();

        var request = new SyncRequest(state.DeviceId, state.CursorFor(accountId), pushedChanges, pushedTombstones);

        var result = await api.ExchangeAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return SyncOutcome.Incomplete(MapFailure(result.Error!.Failure));
        }

        try
        {
            return await ReconcileAsync(accountId, result.Value!, pushedChanges, pushedTombstones, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // The delta could not be persisted; the cursor was not advanced, so the same delta is
            // fetched again on the next cycle. Nothing is lost.
            return SyncOutcome.Incomplete(SyncStatus.StorageUnavailable);
        }
    }

    private async Task<SyncOutcome> ReconcileAsync(
        Guid accountId,
        SyncResponse response,
        List<SyncChange> pushedChanges,
        List<SyncTombstone> pushedTombstones,
        CancellationToken cancellationToken)
    {
        // Re-read so a write made while the request was in flight is reconciled rather than clobbered.
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var state = await local.LoadStateAsync(cancellationToken);

        var pushedVersions = pushedChanges.ToDictionary(c => c.Id, c => c.Version);
        var pendingDeletes = state.Tombstones
            .Where(t => t.AccountId == accountId)
            .Select(t => t.Id)
            .ToHashSet();

        // A push we sent is marked synced only if the copy still holds exactly the version we sent.
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (record.AccountScope == accountId
                && record.Dirty
                && pushedVersions.TryGetValue(record.State.Id, out var sentVersion)
                && record.Version == sentVersion)
            {
                records[i] = record with { Dirty = false };
            }
        }

        var pulled = ApplyPulledChanges(accountId, records, response.Changes, pendingDeletes);
        var deleted = ApplyPulledTombstones(records, response.Tombstones);
        var unclaimed = ApplyUnclaimed(records, response.Unclaimed);

        await local.SaveGroupsAsync(records, cancellationToken);

        var sentTombstoneIds = pushedTombstones.Select(t => t.Id).ToHashSet();
        var remainingTombstones = state.Tombstones
            .Where(t => !(t.AccountId == accountId && sentTombstoneIds.Contains(t.Id)))
            .ToList();
        var cursors = state.Cursors
            .Where(c => c.AccountId != accountId)
            .Append(new AccountSyncCursor(accountId, response.Cursor))
            .ToList();

        await local.SaveStateAsync(state with { Cursors = cursors, Tombstones = remainingTombstones }, cancellationToken);

        return new SyncOutcome(SyncStatus.Synced, pushedChanges.Count, pulled, deleted, unclaimed, response.Collisions.Count);
    }

    private static int ApplyPulledChanges(
        Guid accountId,
        List<SyncedGroupRecord> records,
        IReadOnlyList<SyncChange> changes,
        HashSet<Guid> pendingDeletes)
    {
        var applied = 0;
        foreach (var change in changes)
        {
            // Profile documents ride the same cycle in a later slice; groups are all this engine
            // produces or consumes today. An unknown kind is left for that slice, not dropped loudly.
            if (change.Kind != SyncKinds.Group || pendingDeletes.Contains(change.Id))
            {
                continue;
            }

            var incoming = change.Body.Deserialize(SyncJsonContext.Default.GroupState);
            if (incoming is null)
            {
                continue;
            }

            var index = records.FindIndex(r => r.State.Id == change.Id);
            if (index >= 0)
            {
                var existing = records[index];

                // Do not overwrite an in-flight local edit with a body it is at least as new as; it
                // will be pushed on the next cycle and wins or loses there, under the server's clock.
                if (existing.Dirty && existing.Version >= change.Version)
                {
                    continue;
                }

                records[index] = new SyncedGroupRecord(incoming, change.Version, accountId, Dirty: false);
            }
            else
            {
                records.Add(new SyncedGroupRecord(incoming, change.Version, accountId, Dirty: false));
            }

            applied++;
        }

        return applied;
    }

    private static int ApplyPulledTombstones(List<SyncedGroupRecord> records, IReadOnlyList<SyncTombstone> tombstones)
    {
        var deleted = 0;
        foreach (var tombstone in tombstones)
        {
            if (records.RemoveAll(r => r.State.Id == tombstone.Id) > 0)
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static int ApplyUnclaimed(List<SyncedGroupRecord> records, IReadOnlyList<SyncUnclaimed> unclaimed)
    {
        var dropped = 0;
        foreach (var marker in unclaimed)
        {
            var index = records.FindIndex(r => r.State.Id == marker.Id);
            if (index >= 0 && records[index].AccountScope is not null)
            {
                // Un-claim keeps the local copy and makes it account-free again — it is not a delete.
                records[index] = records[index] with { AccountScope = null, Dirty = false };
                dropped++;
            }
        }

        return dropped;
    }

    private static SyncStatus MapFailure(ApiFailure failure) => failure switch
    {
        ApiFailure.Unreachable => SyncStatus.Offline,
        ApiFailure.Unauthorized => SyncStatus.SignedOut,
        _ => SyncStatus.Failed,
    };
}
