using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.LocalStore.Linking;

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
/// <param name="local">The local sync store holding the queued retractions of deleted groups.</param>
/// <param name="linking">The linking API the queued retractions are sent to.</param>
public sealed class SyncCoordinator(
    SyncService sync,
    OfflineProfileWriter profile,
    LinkResolutionApplier resolutions,
    AssignmentPublisher publisher,
    IGroupRepository groups,
    ILocalSyncStore local,
    ILinkingApi linking)
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

        await RetractDeletedGroupsAsync(cancellationToken);
        await PublishGroupsAsync(cancellationToken);

        return outcome;
    }

    // Sends the retractions queued by deleting a group that had linked members (ADR-0077), so a deleted
    // group leaves every linked member's My assignments / My wishlists. Best-effort: one that fails (offline,
    // server error) stays queued for the next cycle; the server side is idempotent.
    private async Task RetractDeletedGroupsAsync(CancellationToken cancellationToken)
    {
        var state = await local.LoadStateAsync(cancellationToken);
        if (state.Retractions is not { Count: > 0 } queued)
        {
            return;
        }

        var remaining = new List<Guid>();
        foreach (var groupId in queued)
        {
            var result = await linking.RetractGroupAsync(groupId, cancellationToken);
            if (!result.IsSuccess)
            {
                remaining.Add(groupId);
            }
        }

        // Re-read before writing: the group cycle above may have saved the state (cursors, tombstones).
        var latest = await local.LoadStateAsync(cancellationToken);
        var stillQueued = (latest.Retractions ?? []).Where(id => remaining.Contains(id) || !queued.Contains(id)).ToList();
        await local.SaveStateAsync(latest with { Retractions = stillQueued }, cancellationToken);
    }

    // Best-effort publication of every group's linking projection: its event registry (so linked members
    // can enumerate events for their wishlists) and each drawn event's linked-giver rows. A group with no
    // linked members publishes nothing (the account-free and fully-unlinked cases stay inert), and a
    // re-publish of unchanged data is a server no-op, so running this each cycle is what backfills a group
    // the first time one of its members links (FR-029, ADR-0042).
    //
    // Only groups this device organizes are published (ADR-0080, Q4): a group it only joined through a join
    // link is the organizer's to publish, and the server refuses anyone else. A shared-by-link group carries
    // its admin token, which proves this device organizes it.
    private async Task PublishGroupsAsync(CancellationToken cancellationToken) =>
        await publisher.PublishOrganizedAsync(
            await groups.ListAsync(cancellationToken),
            await local.LoadGroupsAsync(cancellationToken),
            cancellationToken);
}
