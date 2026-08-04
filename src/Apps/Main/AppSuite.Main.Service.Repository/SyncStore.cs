using System.Globalization;
using System.Text.Json;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppSuite.Main.Service.Repository;

/// <summary>
/// The EF Core implementation of <see cref="ISyncStore"/> over the <c>sync</c> schema (FEAT-004).
/// </summary>
/// <remarks>
/// <para>
/// One cycle is one transaction: incoming changes are applied and a per-account monotonic
/// <c>server_seq</c> assigned, then the delta above the caller's cursor is read back, then the
/// device's cursor is recorded — atomically, so a device never advances its cursor past changes it
/// did not receive.
/// </para>
/// <para>
/// The aggregate is the unit; there is no field-wise merge. When two devices edit the same aggregate
/// from a common ancestor, the later arrival at the server wins in full and the earlier is discarded
/// in full (last-write-wins by <c>server_modified_at</c>, ADR-0016). The discard is counted through
/// <see cref="ISyncTelemetry"/> — never silent.
/// </para>
/// </remarks>
/// <param name="context">The sync database context.</param>
/// <param name="telemetry">The collision escalation hook (ADR-0012).</param>
/// <param name="timeProvider">The server clock — the only ordering authority (never device time).</param>
public sealed class SyncStore(SyncDbContext context, ISyncTelemetry telemetry, TimeProvider timeProvider) : ISyncStore
{
    // The only kind an un-claim can apply to: a profile is per-account and always synced, never removed
    // from the account. Platform still stores the kind opaquely; this is the fallback when a marker is
    // written for an id whose aggregate row is already gone.
    private const string GroupKind = "group";

    /// <inheritdoc />
    public async Task<SyncResponse> ExchangeAsync(
        Guid accountId,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var changes = request.Changes ?? [];
        var tombstones = request.Tombstones ?? [];
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var collisions = new List<SyncCollision>();
        var incomingCount = changes.Count + tombstones.Count;

        if (incomingCount > 0)
        {
            // Reserve a contiguous block of sequences up front under a single atomic increment. A
            // skipped item (a resurrection) simply leaves its number unused — a gap is harmless
            // because the cursor is "seq greater than mine", never "the count of sequences".
            var blockEnd = await AllocateSequenceBlockAsync(accountId, incomingCount, cancellationToken)
                .ConfigureAwait(false);
            var seq = blockEnd - incomingCount + 1;

            foreach (var change in changes)
            {
                seq = await ApplyChangeAsync(accountId, change, seq, now, collisions, cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var tombstone in tombstones)
            {
                seq = await ApplyTombstoneAsync(accountId, tombstone, seq, cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var appliedAggregates = await context.Aggregates
            .Where(a => a.AccountId == accountId && a.ServerSeq > request.Cursor)
            .OrderBy(a => a.ServerSeq)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var appliedMarkers = await context.Markers
            .Where(m => m.AccountId == accountId && m.ServerSeq > request.Cursor)
            .OrderBy(m => m.ServerSeq)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var newCursor = await ComputeHighWaterMarkAsync(accountId, request.Cursor, cancellationToken)
            .ConfigureAwait(false);

        await UpsertDeviceAsync(accountId, request.DeviceId, newCursor, now, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var outChanges = appliedAggregates
            .Select(a => new SyncChange(a.Kind, a.AggregateId, a.Version, ParseBody(a.Body)))
            .ToList();
        var outTombstones = appliedMarkers
            .Where(m => m.MarkerType == SyncMarkerType.Tombstone)
            .Select(m => new SyncTombstone(m.Kind, m.AggregateId, m.Version))
            .ToList();
        var outUnclaimed = appliedMarkers
            .Where(m => m.MarkerType == SyncMarkerType.Unclaim)
            .Select(m => new SyncUnclaimed(m.AggregateId))
            .ToList();

        return new SyncResponse(newCursor, outChanges, outTombstones, outUnclaimed, collisions);
    }

    /// <inheritdoc />
    public async Task UnclaimAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await context.Aggregates
            .FindAsync([accountId, groupId], cancellationToken).ConfigureAwait(false);
        var marker = await context.Markers
            .FindAsync([accountId, groupId], cancellationToken).ConfigureAwait(false);

        // A delete already stands for this id and outranks an un-claim: the tombstone removes the copy
        // on every device, and turning it into a keep-the-copy marker would resurrect the group on a
        // device that had not yet applied the delete (NFR-008). The server copy is already gone, so the
        // un-claim's own effect — no server copy — already holds; leave the tombstone untouched.
        if (marker is { MarkerType: SyncMarkerType.Tombstone })
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var seq = await AllocateSequenceBlockAsync(accountId, 1, cancellationToken).ConfigureAwait(false);
        var kind = existing?.Kind ?? marker?.Kind ?? GroupKind;

        // Deleting the aggregate row removes its in-body assignments with it — the sync store keeps no
        // separate assignment table (ADR-0020).
        if (existing is not null)
        {
            context.Aggregates.Remove(existing);
        }

        if (marker is null)
        {
            context.Markers.Add(new SyncMarker
            {
                AccountId = accountId,
                AggregateId = groupId,
                Kind = kind,
                Version = existing?.Version ?? 0,
                ServerSeq = seq,
                MarkerType = SyncMarkerType.Unclaim,
            });
        }
        else
        {
            // Refresh an existing un-claim marker's sequence so a device that registered after the first
            // un-claim still pulls it (retention only prunes below every active device's cursor).
            marker.Kind = kind;
            marker.Version = existing?.Version ?? marker.Version;
            marker.ServerSeq = seq;
            marker.MarkerType = SyncMarkerType.Unclaim;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prunes tombstones and un-claim markers that every active device has already seen — those with a
    /// sequence at or below the slowest active device's cursor (FEAT-004). A device gone quiet since
    /// <paramref name="staleBefore"/> is aged out of the calculation, so a one-time sign-in cannot
    /// freeze retention forever; with no active device, nothing is pruned.
    /// </summary>
    /// <param name="accountId">The account whose markers are being swept.</param>
    /// <param name="staleBefore">Devices last seen before this instant are treated as gone.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of markers removed.</returns>
    public async Task<int> PruneMarkersAsync(
        Guid accountId,
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        var activeDevices = context.Devices
            .Where(d => d.AccountId == accountId && d.LastSeenAt >= staleBefore);

        if (!await activeDevices.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var minCursor = await activeDevices
            .MinAsync(d => d.LastCursor, cancellationToken).ConfigureAwait(false);

        return await context.Markers
            .Where(m => m.AccountId == accountId && m.ServerSeq <= minCursor)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ApplyChangeAsync(
        Guid accountId,
        SyncChange change,
        long seq,
        DateTimeOffset now,
        List<SyncCollision> collisions,
        CancellationToken cancellationToken)
    {
        var bodyText = change.Body.GetRawText();
        var marker = await context.Markers
            .FindAsync([accountId, change.Id], cancellationToken).ConfigureAwait(false);

        // A push that would resurrect an aggregate this account has already deleted is dropped —
        // unless it is a genuine re-creation past the tombstone's version (no resurrection: FEAT-004).
        if (marker is { MarkerType: SyncMarkerType.Tombstone } && change.Version <= marker.Version)
        {
            return seq;
        }

        var existing = await context.Aggregates
            .FindAsync([accountId, change.Id], cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            // A re-created or claimed aggregate clears any stale marker standing for its id.
            if (marker is not null)
            {
                context.Markers.Remove(marker);
            }

            context.Aggregates.Add(new SyncAggregate
            {
                AccountId = accountId,
                AggregateId = change.Id,
                Kind = change.Kind,
                Body = bodyText,
                Version = change.Version,
                ServerSeq = seq,
                ServerModifiedAt = now,
            });

            return seq + 1;
        }

        if (change.Version > existing.Version)
        {
            // Clean fast-forward: the pusher held the stored version and moved past it.
            existing.Kind = change.Kind;
            existing.Body = bodyText;
            existing.Version = change.Version;
            existing.ServerSeq = seq;
            existing.ServerModifiedAt = now;
            return seq + 1;
        }

        // Concurrent edit: the incoming push arrives later by the server clock, so it survives in
        // full and the stored body is discarded in full. The loss is bounded and counted (ADR-0016).
        collisions.Add(new SyncCollision(change.Kind, change.Id));
        telemetry.RecordCollision(change.Kind, change.Id);
        existing.Kind = change.Kind;
        existing.Body = bodyText;
        existing.Version = Math.Max(change.Version, existing.Version);
        existing.ServerSeq = seq;
        existing.ServerModifiedAt = now;
        return seq + 1;
    }

    private async Task<long> ApplyTombstoneAsync(
        Guid accountId,
        SyncTombstone tombstone,
        long seq,
        CancellationToken cancellationToken)
    {
        var existing = await context.Aggregates
            .FindAsync([accountId, tombstone.Id], cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            context.Aggregates.Remove(existing);
        }

        var marker = await context.Markers
            .FindAsync([accountId, tombstone.Id], cancellationToken).ConfigureAwait(false);
        if (marker is null)
        {
            context.Markers.Add(new SyncMarker
            {
                AccountId = accountId,
                AggregateId = tombstone.Id,
                Kind = tombstone.Kind,
                Version = tombstone.Version,
                ServerSeq = seq,
                MarkerType = SyncMarkerType.Tombstone,
            });
        }
        else
        {
            marker.Kind = tombstone.Kind;
            marker.Version = tombstone.Version;
            marker.ServerSeq = seq;
            marker.MarkerType = SyncMarkerType.Tombstone;
        }

        return seq + 1;
    }

    private async Task UpsertDeviceAsync(
        Guid accountId,
        string deviceId,
        long cursor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .FindAsync([accountId, deviceId], cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            context.Devices.Add(new SyncDevice
            {
                AccountId = accountId,
                DeviceId = deviceId,
                LastCursor = cursor,
                LastSeenAt = now,
            });
        }
        else
        {
            device.LastCursor = cursor;
            device.LastSeenAt = now;
        }
    }

    private async Task<long> ComputeHighWaterMarkAsync(
        Guid accountId,
        long fallback,
        CancellationToken cancellationToken)
    {
        var aggregateMax = await context.Aggregates
            .Where(a => a.AccountId == accountId)
            .Select(a => (long?)a.ServerSeq)
            .MaxAsync(cancellationToken).ConfigureAwait(false) ?? 0;

        var markerMax = await context.Markers
            .Where(m => m.AccountId == accountId)
            .Select(m => (long?)m.ServerSeq)
            .MaxAsync(cancellationToken).ConfigureAwait(false) ?? 0;

        return Math.Max(fallback, Math.Max(aggregateMax, markerMax));
    }

    private async Task<long> AllocateSequenceBlockAsync(Guid accountId, int count, CancellationToken cancellationToken)
    {
        // The ON CONFLICT UPDATE takes a row lock, so two concurrent cycles on one account serialise
        // here rather than both reading the same counter — the guarantee behind per-account monotonic
        // sequences. Returns the new high watermark; the reserved block is the last <paramref
        // name="count"/> values ending at it.
        //
        // Run as a raw DbCommand rather than through SqlQuery: an INSERT ... RETURNING is not
        // composable, and EF's SqlQuery wraps its argument in a subquery to apply the LINQ operator.
        // The command is enlisted in the cycle's transaction so the row lock is held to commit.
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sync.account_counters ("AccountId", "NextSeq")
            VALUES (@account, @count)
            ON CONFLICT ("AccountId") DO UPDATE SET "NextSeq" = sync.account_counters."NextSeq" + @count
            RETURNING "NextSeq"
            """;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        var accountParameter = command.CreateParameter();
        accountParameter.ParameterName = "account";
        accountParameter.Value = accountId;
        command.Parameters.Add(accountParameter);

        var countParameter = command.CreateParameter();
        countParameter.ParameterName = "count";
        countParameter.Value = (long)count;
        command.Parameters.Add(countParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static JsonElement ParseBody(string bodyJson)
    {
        // Clone so the element outlives the document it was parsed from — it is serialised into the
        // response after this method returns.
        using var document = JsonDocument.Parse(bodyJson);
        return document.RootElement.Clone();
    }
}
