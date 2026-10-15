using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore;

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
        var preferences = await local.LoadPreferencesAsync(cancellationToken);

        var pushedChanges = records
            .Where(r => r.AccountScope == accountId && r.Dirty)
            .Select(r => new SyncChange(
                SyncKinds.Group,
                r.State.Id,
                r.Version,
                JsonSerializer.SerializeToElement(r.State, SyncJsonContext.Default.GroupState)))
            .ToList();

        // The per-account UI preferences ride the same opaque sync store as a distinct "preferences"
        // kind (ADR-0041). Only a dirty document scoped to this account is pushed, and it is kept out of
        // the group push list so the group counts the sync indicator reports are unaffected.
        var preferencesPush = PreferencesPushFor(accountId, preferences);

        var pushedTombstones = state.Tombstones
            .Where(t => t.AccountId == accountId)
            .Select(t => new SyncTombstone(t.Kind, t.Id, t.Version))
            .ToList();

        var outgoingChanges = preferencesPush is null ? pushedChanges : [.. pushedChanges, preferencesPush];
        var request = new SyncRequest(state.DeviceId, state.CursorFor(accountId), outgoingChanges, pushedTombstones);

        var result = await api.ExchangeAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return SyncOutcome.Incomplete(MapFailure(result.Error!.Failure));
        }

        try
        {
            return await ReconcileAsync(
                accountId, result.Value!, pushedChanges, pushedTombstones, preferencesPush?.Version, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // The delta could not be persisted; the cursor was not advanced, so the same delta is
            // fetched again on the next cycle. Nothing is lost.
            return SyncOutcome.Incomplete(SyncStatus.StorageUnavailable);
        }
    }

    /// <summary>
    /// Removes a group from the account (FR-026): the out-of-cycle counterpart to a sync. Unlike an
    /// offline edit, this requires a connection — it deletes the server copy and records an un-claim
    /// marker for the account's other devices. The local copy is <strong>kept</strong> and becomes
    /// account-free again; it is not a delete.
    /// </summary>
    /// <remarks>
    /// The server is called first and the local account scope cleared only once it confirms, so the
    /// scope is never dropped with an orphaned server copy behind it. If the server is unreachable the
    /// removal is unavailable and nothing local changes. Idempotent: removing a group already account-free
    /// here (an un-claim from another device arrived first) still confirms against the server and leaves
    /// the kept local copy untouched, with no error.
    /// </remarks>
    /// <param name="accountId">The signed-in account the group is being removed from.</param>
    /// <param name="groupId">The group to remove from the account.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How it ended: <see cref="SyncStatus.Synced"/> once removed, or why it could not be.</returns>
    public async Task<SyncOutcome> UnclaimAsync(
        Guid accountId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var result = await api.UnclaimAsync(groupId, cancellationToken);
        if (!result.IsSuccess)
        {
            return SyncOutcome.Incomplete(MapFailure(result.Error!.Failure));
        }

        try
        {
            var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
            var index = records.FindIndex(r => r.State.Id == groupId);
            if (index < 0 || records[index].AccountScope != accountId)
            {
                // Nothing scoped to this account to drop — the copy is kept exactly as it is. The server
                // confirmed the removal, so this is a success, not an error (idempotent un-claim).
                return new SyncOutcome(SyncStatus.Synced, 0, 0, 0, Unclaimed: 0, 0);
            }

            // Back to "On this device" as well as account-free: Mode is the authoritative intent
            // (ADR-0073), and the auto-claim re-claims any Mode == Account group on the next cycle.
            records[index] = records[index] with { AccountScope = null, Dirty = false, Mode = StorageMode.OnThisDevice, PendingClaimAccount = null };
            await local.SaveGroupsAsync(records, cancellationToken);

            return new SyncOutcome(SyncStatus.Synced, 0, 0, 0, Unclaimed: 1, 0);
        }
        catch (StorageUnavailableException)
        {
            // The server copy is already gone; the local scope could not be cleared. The kept local copy
            // is untouched (no data lost); the next successful cycle reconciles the scope.
            return SyncOutcome.Incomplete(SyncStatus.StorageUnavailable);
        }
    }

    /// <summary>
    /// Claims the given account-free local groups into the account (FR-025): the out-of-cycle first
    /// upload the claim screen drives. Each group is uploaded through its own <c>/claim</c> route and,
    /// only once the server confirms, marked account-scoped locally — its copy kept as a cache, never
    /// replaced or removed (the action is additive). Groups already scoped, or not held locally, are
    /// skipped silently.
    /// </summary>
    /// <remarks>
    /// Groups are claimed one at a time and in order, so a claim that fails partway leaves the groups
    /// that already arrived account-scoped and the rest purely local (FR-025). A retry re-claims only
    /// the ones that did not arrive; re-claiming one that did updates it rather than duplicating it,
    /// because ids are client-minted (ADR-0008). Nothing local is ever removed — an unreachable server
    /// or a refused local write ends the claim without touching a group's usable local copy (NFR-001).
    /// </remarks>
    /// <param name="accountId">The signed-in account the groups are claimed into.</param>
    /// <param name="groupIds">The groups to claim; account-free local groups among them are uploaded.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How far it got: <see cref="SyncStatus.Synced"/> if every requested group was claimed, else why it stopped.</returns>
    public async Task<ClaimOutcome> ClaimAsync(
        Guid accountId,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);

        var requested = groupIds.Count;
        if (requested == 0)
        {
            return new ClaimOutcome(SyncStatus.Synced, 0, 0);
        }

        List<SyncedGroupRecord> records;
        try
        {
            records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        }
        catch (StorageUnavailableException)
        {
            return ClaimOutcome.Incomplete(SyncStatus.StorageUnavailable, 0, requested);
        }

        var claimed = 0;
        foreach (var groupId in groupIds)
        {
            var index = records.FindIndex(r => r.State.Id == groupId);

            // Only a local group no account has can be claimed; anything already scoped (or not held
            // here) is left exactly as it is. Nor is a shared group whose admin an account claimed — it
            // lives in the shared-group store — or one marked "My account" for a different account
            // (ADR-0078).
            if (index < 0
                || records[index].AccountScope is not null
                || records[index].AdminAccount is not null
                || (records[index].PendingClaimAccount is { } marked && marked != accountId))
            {
                continue;
            }

            var record = records[index];
            var body = JsonSerializer.SerializeToElement(record.State, SyncJsonContext.Default.GroupState);
            var result = await api.ClaimAsync(groupId, new ClaimGroupRequest(record.Version, body), cancellationToken);
            if (!result.IsSuccess)
            {
                // Persist the groups already claimed so the arrived ones stay account-scoped and a retry
                // re-claims only the rest (FR-025). The failing group keeps its account-free local copy.
                await PersistIfAnyClaimedAsync(records, claimed, cancellationToken).ConfigureAwait(false);
                return ClaimOutcome.Incomplete(MapFailure(result.Error!.Failure), claimed, requested);
            }

            // Stored on the server and account-scoped now; the local copy is kept as a cache. Not dirty —
            // the claim route uploaded exactly this version, so the next cycle re-pulls it harmlessly. Also
            // record Mode = Account so the storage mode reflects reality (FR-038): the claim path is the
            // one place a group actually becomes account-owned, and the mode must track it.
            records[index] = record with
            {
                AccountScope = accountId,
                Dirty = false,
                Mode = StorageMode.Account,
                PendingClaimAccount = null,
            };
            claimed++;
        }

        try
        {
            await local.SaveGroupsAsync(records, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // Every selected group reached the server, but the local scope could not be recorded. The
            // groups are safe on the server; a later cycle re-scopes their kept local copies.
            return ClaimOutcome.Incomplete(SyncStatus.StorageUnavailable, claimed, requested);
        }

        return new ClaimOutcome(SyncStatus.Synced, claimed, requested);
    }

    /// <summary>
    /// Records that a local group should live on the signed-in account (FR-038): sets its storage mode to
    /// <see cref="StorageMode.Account"/> so the next sync cycle's auto-claim uploads and account-scopes it.
    /// The account is recorded with the intent, so until the claim the group is hidden while signed out and
    /// never shown to or claimed into another account on this device (ADR-0078).
    /// </summary>
    /// <remarks>
    /// Local-only and best-effort — the actual upload happens on the claim cycle (so it works offline and
    /// retries), which is why this only marks intent rather than calling the server. A group that is not
    /// held here, already account-scoped, or not currently "On this device" (e.g. shared-by-link) is left
    /// unchanged: the account intent only applies to a plain local group.
    /// </remarks>
    /// <param name="accountId">The signed-in account the group is marked for.</param>
    /// <param name="groupId">The local group to mark for the account.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task MarkForAccountAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        List<SyncedGroupRecord> records;
        try
        {
            records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        }
        catch (StorageUnavailableException)
        {
            return;
        }

        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0 || records[index].AccountScope is not null || records[index].Mode != StorageMode.OnThisDevice)
        {
            return;
        }

        records[index] = records[index] with { Mode = StorageMode.Account, PendingClaimAccount = accountId };

        try
        {
            await local.SaveGroupsAsync(records, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // Best-effort: a lost write just means the group stays "On this device" until re-marked.
        }
    }

    private async Task PersistIfAnyClaimedAsync(
        IReadOnlyList<SyncedGroupRecord> records,
        int claimed,
        CancellationToken cancellationToken)
    {
        if (claimed == 0)
        {
            return;
        }

        try
        {
            await local.SaveGroupsAsync(records, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // The scopes could not be written down, but the groups are on the server; a later cycle
            // reconciles their local scope. Nothing local is lost.
        }
    }

    // Build the preferences push for the cycle: null unless the local document is dirty and scoped to the
    // signed-in account. Only the pristine body crosses the wire; the account id is the aggregate id under
    // which the one-per-account document syncs (ADR-0041).
    private static SyncChange? PreferencesPushFor(Guid accountId, SyncedPreferencesRecord? preferences)
    {
        if (preferences is not { Dirty: true } || preferences.AccountScope != accountId)
        {
            return null;
        }

        return new SyncChange(
            SyncKinds.Preferences,
            accountId,
            preferences.Version,
            JsonSerializer.SerializeToElement(preferences.State, SyncJsonContext.Default.PreferencesState));
    }

    private async Task<SyncOutcome> ReconcileAsync(
        Guid accountId,
        SyncResponse response,
        List<SyncChange> pushedChanges,
        List<SyncTombstone> pushedTombstones,
        long? preferencesSentVersion,
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

        // Reconcile the UI preferences before the cursor is advanced, so a storage failure here re-fetches
        // the same delta next cycle rather than skipping a pulled preferences change (ADR-0041).
        await ReconcilePreferencesAsync(accountId, response, preferencesSentVersion, cancellationToken);

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

    // Reconcile the per-account UI preferences alongside the group cycle (ADR-0041): confirm a pushed
    // edit synced, then adopt another device's edit under the same last-write-wins rule the groups follow.
    private async Task ReconcilePreferencesAsync(
        Guid accountId,
        SyncResponse response,
        long? sentVersion,
        CancellationToken cancellationToken)
    {
        // Re-read so an edit made while the request was in flight is reconciled, not clobbered.
        var current = await local.LoadPreferencesAsync(cancellationToken);

        // A push we sent is marked synced only if the copy still holds exactly the version we sent.
        if (sentVersion is { } sent
            && current is { Dirty: true } dirty
            && dirty.AccountScope == accountId
            && dirty.Version == sent)
        {
            current = dirty with { Dirty = false };
            await local.SavePreferencesAsync(current, cancellationToken);
        }

        var incoming = response.Changes
            .LastOrDefault(c => c.Kind == SyncKinds.Preferences && c.Id == accountId);
        if (incoming is null)
        {
            return;
        }

        // Do not overwrite an in-flight local edit with a body it is at least as new as; it will be
        // pushed on the next cycle and win or lose there, under the server's clock.
        if (current is { Dirty: true } pending && pending.Version >= incoming.Version)
        {
            return;
        }

        var body = incoming.Body.Deserialize(SyncJsonContext.Default.PreferencesState);
        if (body is not null)
        {
            await local.SavePreferencesAsync(
                new SyncedPreferencesRecord(body, incoming.Version, accountId, Dirty: false),
                cancellationToken);
        }
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
            // Groups are all this engine produces or consumes over the sync store. A profile change is
            // never carried here (ADR-0035 keeps profile on the authoritative /profile endpoint, not the
            // opaque sync store); an unknown kind is left, not dropped loudly, for whoever adds it.
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
                // The intent goes back to "On this device" too, or this device's auto-claim (Mode ==
                // Account, ADR-0073) would undo the removal made on the other device.
                records[index] = records[index] with { AccountScope = null, Dirty = false, Mode = StorageMode.OnThisDevice, PendingClaimAccount = null };
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
