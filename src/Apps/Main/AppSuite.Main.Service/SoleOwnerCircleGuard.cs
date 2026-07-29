using AppSuite.Application;
using AppSuite.Circles.Core.Application;
using AppSuite.Main.Service.Core.Domain;

namespace AppSuite.Main.Service;

/// <summary>
/// Refuses to delete an account that still owns a circle, and names the circles (FR-015, FR-022).
/// </summary>
/// <param name="sender">The application-layer sender.</param>
/// <remarks>
/// <para>
/// This is the guard M1 built <see cref="IAccountDeletionGuard"/> for, and it closes the acceptance
/// criterion FR-015 has carried since then. It reads through <c>GetSoleOwnedCirclesQuery</c> rather
/// than touching <c>ICircleRepository</c>: the query exists for exactly this, and the host reaches
/// the Circles context through its application layer everywhere else.
/// </para>
/// <para>
/// <strong>It blocks on every circle the account owns, not only the ones nobody else could own.</strong>
/// The narrower reading is tempting — the query is called <em>sole owned</em> and the feature spec
/// says "sole owner" — but it is wrong: for a circle that has other members, deletion would run
/// <c>PurgeAccount</c> over the owner's own membership and leave the circle with no owner at all.
/// Nothing in the domain defines what an ownerless circle means, precisely because
/// <c>Circle.Leave</c> makes sure one can never occur; the guard has to hold the same line that
/// <c>Leave</c> does. That is why the query returns every owned circle with a count beside it, and
/// why the count — not the presence of a row — decides the way out.
/// </para>
/// <para>
/// So each blocker carries the resolution that fits it, and they are the same two codes
/// <c>Leave</c> already returns: hand the circle to somebody else when there is somebody to hand it
/// to (FR-020), otherwise delete it (FR-022). The UI can then offer the right action per circle
/// instead of merely describing the problem.
/// </para>
/// </remarks>
public sealed class SoleOwnerCircleGuard(ISender sender) : IAccountDeletionGuard
{
    /// <inheritdoc />
    public async Task<AccountDeletionRefusal?> CheckAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var owned = await sender
            .Send(new GetSoleOwnedCirclesQuery(userId), cancellationToken)
            .ConfigureAwait(false);

        if (owned.Count == 0)
        {
            return null;
        }

        var blockers = owned
            .Select(circle => new AccountDeletionBlocker(
                circle.Id,
                circle.Name,
                circle.OtherAccountBackedPeople > 0
                    ? "owner_must_transfer_ownership"
                    : "owner_must_delete_circle"))
            .ToArray();

        return new AccountDeletionRefusal(
            "circles_still_owned",
            "Hand over or delete the circles you own before deleting your account.",
            blockers);
    }
}
