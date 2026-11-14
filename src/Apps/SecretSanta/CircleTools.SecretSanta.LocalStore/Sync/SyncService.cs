// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// Runs one account sync cycle for a signed-in account (FEAT-004): push the preferences document if it is dirty,
/// apply the server delta above the cursor, and advance the cursor. Online groups have their own sync
/// (ADR-0086); this cycle carries the per-account preferences only (ADR-0041).
/// </summary>
/// <remarks>
/// <para>
/// Only a document scoped to the calling account is pushed, and only to that account (FR-024). A no-op cycle
/// (nothing dirty, cursor current) carries no bodies in either direction (NFR-008).
/// </para>
/// <para>
/// If the server is unreachable or a local write is refused, the cycle ends without advancing the cursor, so the
/// delta is simply re-fetched next time — an interrupted cycle loses nothing and duplicates nothing (NFR-008). The
/// store is re-read after the round-trip, so an edit made while the request was in flight is reconciled, not
/// clobbered.
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
        var state = await local.LoadStateAsync(cancellationToken);
        var preferences = await local.LoadPreferencesAsync(cancellationToken);
        var push = PreferencesPushFor(accountId, preferences);

        var request = new SyncRequest(state.DeviceId, state.CursorFor(accountId), push is null ? [] : [push], []);
        var result = await api.ExchangeAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return SyncOutcome.Incomplete(MapFailure(result.Error!.Failure));
        }

        try
        {
            var response = result.Value!;
            var pulled = await ReconcilePreferencesAsync(accountId, response, push?.Version, cancellationToken);

            // Re-read the state: another cycle may have moved another account's cursor meanwhile.
            var current = await local.LoadStateAsync(cancellationToken);
            var cursors = current.Cursors
                .Where(c => c.AccountId != accountId)
                .Append(new AccountSyncCursor(accountId, response.Cursor))
                .ToList();
            await local.SaveStateAsync(current with { Cursors = cursors }, cancellationToken);

            return new SyncOutcome(SyncStatus.Synced, push is null ? 0 : 1, pulled, response.Collisions.Count);
        }
        catch (StorageUnavailableException)
        {
            // The delta could not be persisted; the cursor was not advanced, so the same delta is fetched again
            // on the next cycle. Nothing is lost.
            return SyncOutcome.Incomplete(SyncStatus.StorageUnavailable);
        }
    }

    // Only the pristine body crosses the wire; the account id is the aggregate id under which the one-per-account
    // document syncs (ADR-0041).
    private static SyncChange? PreferencesPushFor(Guid accountId, SyncedPreferencesRecord? preferences) =>
        preferences is not { Dirty: true } || preferences.AccountScope != accountId
            ? null
            : new SyncChange(
                SyncKinds.Preferences,
                accountId,
                preferences.Version,
                JsonSerializer.SerializeToElement(preferences.State, SyncJsonContext.Default.PreferencesState));

    // Confirm a pushed edit synced, then adopt another device's edit under last-write-wins. Returns whether one was
    // pulled.
    private async Task<int> ReconcilePreferencesAsync(
        Guid accountId,
        SyncResponse response,
        long? sentVersion,
        CancellationToken cancellationToken)
    {
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

        var incoming = response.Changes.LastOrDefault(c => c.Kind == SyncKinds.Preferences && c.Id == accountId);

        // Do not overwrite an in-flight local edit with a body it is at least as new as; it will be pushed on the
        // next cycle and win or lose there, under the server's clock.
        if (incoming is null || (current is { Dirty: true } pending && pending.Version >= incoming.Version))
        {
            return 0;
        }

        var body = incoming.Body.Deserialize(SyncJsonContext.Default.PreferencesState);
        if (body is null)
        {
            return 0;
        }

        await local.SavePreferencesAsync(new SyncedPreferencesRecord(body, incoming.Version, accountId, Dirty: false), cancellationToken);
        return 1;
    }

    private static SyncStatus MapFailure(ApiFailure failure) => failure switch
    {
        ApiFailure.Unreachable => SyncStatus.Offline,
        ApiFailure.Unauthorized => SyncStatus.SignedOut,
        _ => SyncStatus.Failed,
    };
}
