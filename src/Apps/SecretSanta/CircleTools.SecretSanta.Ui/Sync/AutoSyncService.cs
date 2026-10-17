using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Runs the account sync <strong>silently in the background</strong> (ADR-0044): the single place that
/// turns the four sync triggers — sign-in, app start while signed in, every change, and reconnect —
/// into a debounced, coalesced call to <see cref="SyncCoordinator"/>. The user never asks for a sync
/// and never waits on one; an offline edit simply flushes the moment connectivity returns.
/// </summary>
/// <remarks>
/// <para>
/// <strong>No blocking UI, no thrown errors.</strong> A failed cycle (offline, server down) is
/// swallowed: the dirty queue survives, so the same delta is simply re-attempted on the next trigger
/// (NFR-008). Editing stays fully available throughout (NFR-001).
/// </para>
/// <para>
/// <strong>Signed out is a no-op.</strong> The account id is read from the auth state at fire time, so
/// a request raised while signed out (e.g. an offline create) resolves to <see cref="Guid.Empty"/> and
/// does nothing — the account-free product is unaffected.
/// </para>
/// <para>
/// <strong>Debounced and coalesced.</strong> A burst of edits collapses to one cycle a short delay
/// after the last one. A change arriving <em>during</em> a running cycle sets a follow-up rather than
/// overlapping, so the latest state is always pushed without concurrent cycles. The sync path never
/// dispatches commands through <c>ISender</c>, so the after-change trigger cannot feed itself.
/// </para>
/// </remarks>
/// <param name="coordinator">The full account-sync coordinator (profile + links + groups + publish).</param>
/// <param name="claimable">Reads which local groups are still account-free, for the auto-claim step.</param>
/// <param name="sync">The sync engine, for claiming account-free groups into the signed-in account.</param>
/// <param name="sharedSync">The shared-by-link engine, reconciling token-addressed groups signed in or out (FEAT-013).</param>
/// <param name="revealSlices">Publishes backend-backed groups' live reveal slices after each reconcile (ADR-0068, FEAT-020).</param>
/// <param name="auth">The current principal, read for the signed-in account id.</param>
/// <param name="connectivity">The connectivity seam, subscribed for the reconnect trigger.</param>
/// <param name="time">The clock, for when the last cycle completed (the app bar's sync indicator).</param>
public sealed class AutoSyncService(
    SyncCoordinator coordinator,
    ClaimableGroupsService claimable,
    SyncService sync,
    SharedSyncService sharedSync,
    RevealSlicePublisher revealSlices,
    AuthenticationStateProvider auth,
    IConnectivity connectivity,
    TimeProvider time) : IDisposable, ISyncSignals
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(750);

    private CancellationTokenSource? _debounce;
    private bool _initialized;
    private bool _syncing;
    private bool _resyncRequested;

    /// <summary>
    /// Set by the sign-in reconciler while its Import / Discard / Cancel dialog is open, so a background
    /// cycle cannot auto-claim (and thereby upload) the very groups the user is deciding to discard. The
    /// reconciler clears it and requests the first cycle itself once the choice is made (ADR-0044).
    /// </summary>
    public bool Reconciling { get; set; }

    /// <summary>
    /// The outcome of the most recent completed group cycle, or <see langword="null"/> before the first
    /// one drains. The shell shows this in its passive status line now that there is no manual "Sync now"
    /// button (ADR-0044). Read it in a <see cref="SyncCompleted"/> handler.
    /// </summary>
    public SyncStatus? LastStatus { get; private set; }

    /// <summary>When the last account cycle completed successfully, or <see langword="null"/> before one did.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>Whether a cycle is running right now — the app bar shows a spinner meanwhile (T3).</summary>
    public bool IsSyncing => _syncing;

    /// <summary>
    /// Raised when a cycle starts, so the app bar's sync indicator can show its spinner. Fired off the render
    /// context; <see cref="SyncCompleted"/> follows when the cycle drains.
    /// </summary>
    public event Action? SyncStarted;

    /// <summary>
    /// Raised when <see cref="LastStatus"/> changes outside a cycle — the device just went offline — so the app
    /// bar's sync indicator says so at once rather than after the next attempt. Fired off the render context.
    /// </summary>
    public event Action? StatusChanged;

    /// <summary>
    /// Raised after a background sync cycle drains, so a listing screen can refresh to show groups the
    /// cycle pulled or claimed. Fired on the app's sync task continuation — subscribers marshal onto the
    /// render context (e.g. <c>InvokeAsync</c>) before touching component state.
    /// </summary>
    public event Action? SyncCompleted;

    /// <summary>
    /// Raised as soon as a sync is requested — i.e. right after any local mutation (the after-change
    /// behavior, ADR-0044), a reconnect, or app start. Unlike <see cref="SyncCompleted"/> it fires
    /// <strong>regardless of sign-in state</strong>, so a signed-out change (e.g. deleting a group) still
    /// tells device-local views like the drawer's quick-access to refresh — a cycle that no-ops while
    /// signed out never reaches <see cref="SyncCompleted"/>. Subscribers marshal onto the render context.
    /// </summary>
    public event Action? LocalChanged;

    /// <summary>
    /// Raised when a cycle kept groups as "On this device" copies because another device moved them out of
    /// the account while this device held unsynced edits (ADR-0073 Amendment 2). Carries the group names, so
    /// the shell tells the user once. Fired off the render context.
    /// </summary>
    public event Action<IReadOnlyList<string>>? KeptAsLocalCopy;

    /// <summary>
    /// Starts listening for reconnects and, if a session is already live, runs the app-start sync.
    /// Called once from the shell's first render, when the JS runtime is available. Idempotent.
    /// </summary>
    /// <returns>A task that completes once listening has started.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        connectivity.Changed += OnConnectivityChanged;
        await connectivity.InitializeAsync();

        // App-start trigger: reconcile on every start, signed in OR out. The shared-by-link cycle
        // (FEAT-013) is account-free and must run for a signed-out co-organizer/participant so a plain
        // refresh pulls a co-admin's changes; without this only a local edit, a reconnect, or (touch-only)
        // pull-to-refresh triggered it. The account cycle self-gates on sign-in, and the whole cycle is a
        // cheap no-op when there are no shared or account groups to reconcile.
        RequestSync();
    }

    /// <summary>
    /// Requests a background sync soon, coalescing a burst into one cycle. Safe to call from any
    /// trigger, signed in or out; signed out it resolves to a no-op at fire time.
    /// </summary>
    public void RequestSync()
    {
        // Tell device-local views (drawer quick-access, listing screens) that the local data may have
        // just changed, before any debounced sync runs and even when signed out (when the cycle no-ops).
        LocalChanged?.Invoke();

        _debounce?.Cancel();
        _debounce?.Dispose();

        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = RunAfterDelayAsync(cts.Token);
    }

    private async Task RunAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return; // Superseded by a newer request; that one carries the sync.
        }

        await SyncNowAsync();
    }

    private async Task SyncNowAsync()
    {
        if (_syncing)
        {
            _resyncRequested = true; // Fold this request into a follow-up after the running cycle.
            return;
        }

        _syncing = true;
        SyncStarted?.Invoke();
        try
        {
            do
            {
                _resyncRequested = false;

                // Shared-by-link groups reconcile signed in OR out — they are addressed by a share
                // token, not scoped to an account (FEAT-013). Runs first, always.
                try
                {
                    await RunSharedCycleAsync();
                }
                catch
                {
                    // Swallowed by design: retried on the next trigger, nothing lost (NFR-008).
                }

                // The account cycle runs only while signed in and not mid-reconcile.
                var accountId = await CurrentAccountAsync();
                if (accountId != Guid.Empty && !Reconciling)
                {
                    try
                    {
                        await RunCycleAsync(accountId);
                    }
                    catch
                    {
                        // Swallowed by design: retried on the next trigger, nothing lost (NFR-008).
                    }
                }
            }
            while (_resyncRequested);
        }
        finally
        {
            _syncing = false;
        }

        SyncCompleted?.Invoke();
    }

    // Reconcile every shared-by-link group this device holds — signed in or out. Each is a single
    // aggregate addressed by its own token; the engine pulls a newer server copy or pushes a dirty
    // local edit (FEAT-013). A group whose online copy is gone falls back to on-this-device inside the
    // engine, so the set naturally shrinks.
    private async Task RunSharedCycleAsync()
    {
        // Account-store groups (AccountScope) sync through the account cycle, not here (ADR-0078 Amendment 3).
        var shared = (await claimable.GetAllAsync())
            .Where(g => g.AccountScope is null && g.Mode is StorageMode.SharedByLink or StorageMode.Account)
            .Select(g => g.Id)
            .ToList();

        foreach (var id in shared)
        {
            await sharedSync.SyncGroupAsync(id);
            // After reconciling, re-publish the group's reveal slices so an already-sent reveal link
            // reflects the latest wishlist/event (ADR-0068). Best-effort and a no-op for a group this
            // device cannot publish (join-only), so it is safe to call for every shared group.
            await revealSlices.PublishAsync(id);
        }
    }

    // One cycle for a signed-in account: first adopt any still-account-free groups into the account
    // (created offline before sign-in and imported, or created while signed in but offline so the
    // inline claim could not reach the server), then run the full sync. Claiming is idempotent and a
    // no-op when there is nothing account-free, so a signed-in device converges on "everything synced"
    // without the user ever managing it (ADR-0044). The reconciler's Discard path runs before this, so
    // groups the user chose to discard are already gone and never claimed.
    private async Task RunCycleAsync(Guid accountId)
    {
        // Discover any groups this account claimed admin of on another device (D2), so they appear and
        // are manageable here too — and attribute any already-held ones to this account. Signed-in only
        // (this cycle), so the bearer is present.
        // Send the owner deletes of "My account" groups deleted on this device first, so discovery below
        // does not see them on the server (it also skips any still queued) — ADR-0078.
        await sharedSync.SendOwnedDeletesAsync(accountId);
        var ownedListed = await sharedSync.MergeOwnedAsync(accountId);

        // Groups this account joined on another device ("keep it on my account") come to this one too (ADR-0080).
        await sharedSync.DiscoverJoinedAsync(accountId);

        // Auto-claim only the groups the user has marked "My account" for this account and that are not yet
        // account-scoped (FR-038, ADR-0073). A group left "On this device" stays local even while signed in
        // — its mode is honoured, not silently synced. Shared-by-link groups live in the token-keyed store
        // and are never M3-claimed, nor is a shared group whose admin an account claimed (ADR-0078). A
        // legacy "My account" record with no account recorded is claimed only once the owned list above was
        // read, so an admin-claimed shared group among them has been recognised and is not uploaded twice.
        var awaitingClaim = (await claimable.GetAwaitingClaimAsync(accountId, includeUnattributed: ownedListed))
            .Select(g => g.Id)
            .ToList();
        if (awaitingClaim.Count > 0)
        {
            await sync.ClaimAsync(accountId, awaitingClaim);
        }

        // Keep the group cycle's outcome so the shell can show it in its passive status line — the same
        // status the removed manual button used to surface.
        var heldBefore = (await claimable.GetAllAsync()).Select(g => g.Id).ToHashSet();
        var outcome = await coordinator.SyncNowAsync(accountId);
        LastStatus = outcome.Status;
        if (outcome.IsSuccess)
        {
            LastSyncedAt = time.GetUtcNow();
        }

        // A group the account cycle just dropped may have moved out of the account store into the shared
        // store as this account's own shared group ("Invite people", ADR-0081). Discovery ran before the drop
        // saw it as already held; look again so it comes back in this cycle, not the next one.
        if ((await claimable.GetAllAsync()).Count(g => heldBefore.Contains(g.Id)) < heldBefore.Count)
        {
            await sharedSync.MergeOwnedAsync(accountId);
        }
        if (outcome.KeptAsLocalCopy is { Count: > 0 } kept)
        {
            KeptAsLocalCopy?.Invoke(kept);
        }
    }

    private void OnConnectivityChanged(bool online)
    {
        if (online)
        {
            RequestSync(); // Reconnect: flush queued edits and pull.
            return;
        }

        LastStatus = SyncStatus.Offline;
        StatusChanged?.Invoke();
    }

    private async Task<Guid> CurrentAccountAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        return state.User.UserId();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        connectivity.Changed -= OnConnectivityChanged;
        _debounce?.Cancel();
        _debounce?.Dispose();
    }
}
