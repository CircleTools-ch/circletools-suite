using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.SecretSanta.Ui;

/// <summary>
/// Runs a full account sync for the Secret-Santa app: the profile reconcile (ADR-0035) and then the
/// group delta cycle (FEAT-004), so a single "Sync now" pushes a queued offline profile edit, adopts
/// another device's preference change, and reconciles groups — everything the account syncs, in one
/// action.
/// </summary>
/// <remarks>
/// The profile reconcile runs first and is best-effort (it never throws), so the returned
/// <see cref="SyncOutcome"/> is the group cycle's — the one the sync indicator already reports. Claim
/// and un-claim stay their own out-of-cycle actions on <see cref="SyncService"/>; only the delta cycle
/// carries the profile.
/// </remarks>
/// <param name="sync">The group sync engine.</param>
/// <param name="profile">The offline profile writer, whose reconcile flushes and pulls the profile.</param>
public sealed class SyncCoordinator(SyncService sync, OfflineProfileWriter profile)
{
    /// <summary>Reconciles the profile, then runs one group sync cycle for the account.</summary>
    /// <param name="accountId">The signed-in account — the only sync scope.</param>
    /// <param name="cancellationToken">Cancels the sync.</param>
    /// <returns>How the group cycle ended and what moved.</returns>
    public async Task<SyncOutcome> SyncNowAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await profile.ReconcileAsync(accountId, cancellationToken);
        return await sync.SyncNowAsync(accountId, cancellationToken);
    }
}
