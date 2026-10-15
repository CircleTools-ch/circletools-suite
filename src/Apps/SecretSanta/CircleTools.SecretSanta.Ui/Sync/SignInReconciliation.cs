using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Remembers that an interactive sign-in still has to be reconciled (ADR-0044, amendment 2026-10-15). It is a
/// service, not component state: signing in can switch the UI language, and the culture boundary rebuilds
/// the whole component tree, including the <c>SignInReconciler</c> and the dialog provider. Kept here, the
/// request survives the rebuild and the new reconciler asks, in the account's language. Earlier the
/// request lived in the component, so a rebuild either lost it or closed the prompt with "no answer".
/// </summary>
/// <param name="auth">The current principal; its anonymous to authenticated transition is a sign-in.</param>
/// <param name="autoSync">The background sync, whose auto-claim waits while a sign-in is pending.</param>
public sealed class SignInReconciliation(AuthenticationStateProvider auth, AutoSyncService autoSync) : IDisposable
{
    private bool _initialized;
    private bool _wasAuthenticated;

    /// <summary>Gets the account of a sign-in still waiting to be reconciled, or <see langword="null"/>.</summary>
    public Guid? Pending { get; private set; }

    /// <summary>Raised when a sign-in starts waiting to be reconciled. Fired off the render context.</summary>
    public event Action? PendingChanged;

    /// <summary>
    /// Starts watching for sign-ins. Idempotent. A session restored at startup is already signed in by
    /// now, so it is not a sign-in to reconcile.
    /// </summary>
    /// <returns>A task that completes once watching has started.</returns>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var state = await auth.GetAuthenticationStateAsync();
        _wasAuthenticated = state.User.Identity?.IsAuthenticated == true;
        auth.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    /// <summary>Marks the pending sign-in as settled (answered, or nothing to ask) and lets the auto-claim run.</summary>
    public void Complete()
    {
        Pending = null;
        autoSync.Reconciling = false;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _ = HandleAsync(task);

    private async Task HandleAsync(Task<AuthenticationState> task)
    {
        AuthenticationState state;
        try
        {
            state = await task;
        }
        catch
        {
            return;
        }

        var isAuthenticated = state.User.Identity?.IsAuthenticated == true;
        var wasAuthenticated = _wasAuthenticated;
        _wasAuthenticated = isAuthenticated;

        if (!isAuthenticated)
        {
            // Signed out before answering: nothing to reconcile any more.
            Complete();
            return;
        }

        // Only an anonymous -> authenticated transition; a token refresh is not a sign-in.
        if (wasAuthenticated)
        {
            return;
        }

        var accountId = state.User.UserId();
        if (accountId == Guid.Empty)
        {
            return;
        }

        Pending = accountId;
        autoSync.Reconciling = true; // Suspend the background auto-claim until the user decides.
        PendingChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose() => auth.AuthenticationStateChanged -= OnAuthenticationStateChanged;
}
