using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.LocalStore.Online;
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
/// <param name="coordinator">The account part of a sync (profile, preferences).</param>
/// <param name="onlineGroups">The online-group sync, run signed in or out (ADR-0086).</param>
/// <param name="auth">The current principal, read for the signed-in account id.</param>
/// <param name="session">Renews a session the server could not be reached for at startup, on every trigger.</param>
/// <param name="connectivity">The connectivity seam, subscribed for the reconnect trigger.</param>
/// <param name="time">The clock, for when the last cycle completed (the app bar's sync indicator).</param>
public sealed class AutoSyncService(
    SyncCoordinator coordinator,
    OnlineGroupSync onlineGroups,
    AuthenticationStateProvider auth,
    AccountSession session,
    IConnectivity connectivity,
    TimeProvider time) : IDisposable, ISyncSignals
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(750);

    private CancellationTokenSource? _debounce;
    private bool _initialized;
    private bool _syncing;
    private bool _resyncRequested;

    // The account the last auth-state change resolved to, so the sign-in trigger fires once per sign-in.
    private Guid _accountId;

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

        // Sign-in trigger: an interactive sign-in must pull the account's groups at once. On a device that
        // never synced, nothing else fires until the next edit or app start, so the list stayed empty until a
        // reload (2026-10-28; the sign-in reconciler that used to request this cycle went with ADR-0086).
        _accountId = await CurrentAccountAsync();
        auth.AuthenticationStateChanged += OnAuthenticationStateChanged;

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

                // A device that started offline has a remembered session but no access token: renew it now that a
                // trigger fired (reconnect, start, change), so the account cycle below can run.
                try
                {
                    await session.ResumeAsync();
                }
                catch
                {
                    // Swallowed by design: still waiting for the server; retried on the next trigger.
                }

                // Online groups reconcile signed in OR out — a link or device key needs no account (ADR-0086).
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

    // Every online group this device holds, signed in or out (ADR-0086): organizers push then pull, participants
    // send their entry then read their view; signed in, the account's groups are discovered too. A group whose
    // online copy is gone becomes a device group (organizer) or disappears (participant) inside the sync.
    private async Task RunSharedCycleAsync()
    {
        var accountId = await CurrentAccountAsync();
        await onlineGroups.SyncAsync(accountId == Guid.Empty ? null : accountId);
    }

    // The account part of a cycle: the profile and the per-account documents (preferences). Its outcome is the
    // shell's passive status line.
    private async Task RunCycleAsync(Guid accountId)
    {
        var outcome = await coordinator.SyncNowAsync(accountId);
        LastStatus = outcome.Status;
        if (outcome.IsSuccess)
        {
            LastSyncedAt = time.GetUtcNow();
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

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state) => _ = OnAccountChangedAsync(state);

    // Signing in, switching account or signing out requests a cycle: the views hide the account's groups on their
    // own, but a group the device holds by link or key that waited while the account's own record was in play comes
    // back only through the cycle (ST-1). Token refreshes re-raise the event for the same account and are ignored.
    private async Task OnAccountChangedAsync(Task<AuthenticationState> state)
    {
        Guid accountId;
        try
        {
            accountId = (await state).User.UserId();
        }
        catch
        {
            return; // Swallowed by design: the next trigger syncs.
        }

        if (accountId == _accountId)
        {
            return;
        }

        _accountId = accountId;
        RequestSync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        auth.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        connectivity.Changed -= OnConnectivityChanged;
        _debounce?.Cancel();
        _debounce?.Dispose();
    }
}
