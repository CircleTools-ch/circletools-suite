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
/// <param name="auth">The current principal, read for the signed-in account id.</param>
/// <param name="connectivity">The connectivity seam, subscribed for the reconnect trigger.</param>
public sealed class AutoSyncService(
    SyncCoordinator coordinator,
    ClaimableGroupsService claimable,
    SyncService sync,
    AuthenticationStateProvider auth,
    IConnectivity connectivity) : IDisposable
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

    /// <summary>
    /// Raised after a background sync cycle drains, so a listing screen can refresh to show groups the
    /// cycle pulled or claimed. Fired on the app's sync task continuation — subscribers marshal onto the
    /// render context (e.g. <c>InvokeAsync</c>) before touching component state.
    /// </summary>
    public event Action? SyncCompleted;

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

        // App-start trigger: a restored session (ADR-0021) skips the sign-in dialog, so pull its data now.
        if (await CurrentAccountAsync() != Guid.Empty)
        {
            RequestSync();
        }
    }

    /// <summary>
    /// Requests a background sync soon, coalescing a burst into one cycle. Safe to call from any
    /// trigger, signed in or out; signed out it resolves to a no-op at fire time.
    /// </summary>
    public void RequestSync()
    {
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
        var accountId = await CurrentAccountAsync();
        if (accountId == Guid.Empty || Reconciling)
        {
            return; // Signed out, or the reconciler owns the first cycle: nothing to do here.
        }

        if (_syncing)
        {
            _resyncRequested = true; // Fold this request into a follow-up after the running cycle.
            return;
        }

        _syncing = true;
        try
        {
            do
            {
                _resyncRequested = false;
                try
                {
                    await RunCycleAsync(accountId);
                }
                catch
                {
                    // Swallowed by design: retried on the next trigger, nothing lost (NFR-008).
                }
            }
            while (_resyncRequested && !Reconciling && await CurrentAccountAsync() != Guid.Empty);
        }
        finally
        {
            _syncing = false;
        }

        SyncCompleted?.Invoke();
    }

    // One cycle for a signed-in account: first adopt any still-account-free groups into the account
    // (created offline before sign-in and imported, or created while signed in but offline so the
    // inline claim could not reach the server), then run the full sync. Claiming is idempotent and a
    // no-op when there is nothing account-free, so a signed-in device converges on "everything synced"
    // without the user ever managing it (ADR-0044). The reconciler's Discard path runs before this, so
    // groups the user chose to discard are already gone and never claimed.
    private async Task RunCycleAsync(Guid accountId)
    {
        var accountFree = (await claimable.GetAccountFreeAsync()).Select(g => g.Id).ToList();
        if (accountFree.Count > 0)
        {
            await sync.ClaimAsync(accountId, accountFree);
        }

        // Keep the group cycle's outcome so the shell can show it in its passive status line — the same
        // status the removed manual button used to surface.
        LastStatus = (await coordinator.SyncNowAsync(accountId)).Status;
    }

    private void OnConnectivityChanged(bool online)
    {
        if (online)
        {
            RequestSync(); // Reconnect: flush queued edits and pull.
        }
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
