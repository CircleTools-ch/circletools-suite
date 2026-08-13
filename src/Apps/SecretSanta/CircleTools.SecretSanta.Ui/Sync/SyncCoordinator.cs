using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Runs a full account sync for the Secret-Santa app: the profile reconcile (ADR-0035), the M4 link
/// resolutions (FEAT-005), the group delta cycle (FEAT-004), and the assignment publication — so a
/// single "Sync now" pushes a queued offline profile edit, adopts another device's preference change,
/// applies any accepted/unlinked members onto the roster, reconciles groups, and makes each drawn
/// group's linked members able to read their assignment.
/// </summary>
/// <remarks>
/// Order matters and each step is best-effort so none aborts the others: resolutions are applied
/// <em>before</em> the group cycle, so a newly-set <c>Member.UserId</c> is pushed by the same cycle;
/// publication runs <em>after</em>, so a member linked this cycle (including the backfill of a group
/// drawn before M4) gets their row. The returned <see cref="SyncOutcome"/> is the group cycle's — the
/// one the sync indicator already reports.
/// </remarks>
/// <param name="sync">The group sync engine.</param>
/// <param name="profile">The offline profile writer, whose reconcile flushes and pulls the profile.</param>
/// <param name="resolutions">Applies pending member-link resolutions onto the local groups.</param>
/// <param name="publisher">Publishes a drawn group's linked-giver assignment rows.</param>
/// <param name="groups">The local group repository, to publish each drawn group.</param>
public sealed class SyncCoordinator(
    SyncService sync,
    OfflineProfileWriter profile,
    LinkResolutionApplier resolutions,
    AssignmentPublisher publisher,
    IGroupRepository groups)
{
    /// <summary>Reconciles the profile and links, runs one group cycle, then publishes drawn groups.</summary>
    /// <param name="accountId">The signed-in account — the only sync scope.</param>
    /// <param name="cancellationToken">Cancels the sync.</param>
    /// <returns>How the group cycle ended and what moved.</returns>
    public async Task<SyncOutcome> SyncNowAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await profile.ReconcileAsync(accountId, cancellationToken);
        await resolutions.ApplyPendingAsync(cancellationToken);

        var outcome = await sync.SyncNowAsync(accountId, cancellationToken);

        await PublishDrawnGroupsAsync(cancellationToken);

        return outcome;
    }

    // Best-effort publication of every drawn group's linked-giver rows. A group with no linked givers
    // publishes nothing (the account-free and fully-unlinked cases stay inert), and a re-publish of an
    // unchanged draw is a server no-op, so running this each cycle is what backfills a group drawn
    // before M4 the first time one of its members links (FR-029).
    private async Task PublishDrawnGroupsAsync(CancellationToken cancellationToken)
    {
        var all = await groups.ListAsync(cancellationToken);
        foreach (var group in all.Where(g => g.CurrentDraw is not null))
        {
            await publisher.PublishAsync(group, cancellationToken);
        }
    }
}
