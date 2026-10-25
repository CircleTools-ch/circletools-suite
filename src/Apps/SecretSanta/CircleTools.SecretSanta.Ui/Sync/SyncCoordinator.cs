using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Runs the account part of a sync for the Secret-Santa app: the profile reconcile (ADR-0035) and the per-account
/// documents of the sync store — the UI preferences (ADR-0041). Groups no longer travel through the sync store;
/// online groups sync through <see cref="LocalStore.Online.OnlineGroupSync"/> (ADR-0086).
/// </summary>
/// <param name="sync">The sync-store cycle.</param>
/// <param name="profile">The offline profile writer, whose reconcile flushes and pulls the profile.</param>
public sealed class SyncCoordinator(SyncService sync, OfflineProfileWriter profile)
{
    /// <summary>Reconciles the profile, then runs one sync-store cycle.</summary>
    /// <param name="accountId">The signed-in account — the only sync scope.</param>
    /// <param name="cancellationToken">Cancels the sync.</param>
    /// <returns>How the cycle ended.</returns>
    public async Task<SyncOutcome> SyncNowAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await profile.ReconcileAsync(accountId, cancellationToken);
        return await sync.SyncNowAsync(accountId, cancellationToken);
    }
}
