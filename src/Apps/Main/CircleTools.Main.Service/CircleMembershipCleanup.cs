using CircleTools.Application;
using CircleTools.Circles.Core.Application;
using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service;

/// <summary>
/// Removes a departing account from every circle it is in — the second half of what account
/// deletion needs (FR-015).
/// </summary>
/// <param name="sender">The application-layer sender.</param>
/// <remarks>
/// <para>
/// The adapter lives in this host rather than in the Circles context because
/// <see cref="IAccountDeletionCleanup"/> is Main's seam and Circles does not reference it — the same
/// direction of dependency that keeps <c>IAccountDirectory</c> and <c>IInvitationMailer</c> here.
/// All it does is turn Main's port into the Circles command that already exists.
/// </para>
/// <para>
/// Ownership is not its problem: <see cref="SoleOwnerCircleGuard"/> has already refused the deletion
/// if the account owns anything, so by the time this runs every remaining membership is one that can
/// simply be dropped. Managed members are untouched — they are circle data and survive the admin who
/// added them (FR-019).
/// </para>
/// </remarks>
public sealed class CircleMembershipCleanup(ISender sender) : IAccountDeletionCleanup
{
    /// <inheritdoc />
    public Task CleanUpAsync(Guid userId, CancellationToken cancellationToken = default) =>
        sender.Send(new PurgeAccountFromCirclesCommand(userId), cancellationToken);
}
