namespace AppSuite.Main.Service.Core.Domain;

/// <summary>
/// Work a context must do when an account is deleted, so nothing is left pointing at it (FR-015).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IAccountDeletionGuard"/> answers <em>may this account be deleted</em>. That is only
/// half of what deletion needs: nothing removed the departing user's rows in other contexts, and
/// without that a deleted account leaves memberships pointing at a user identifier that no longer
/// resolves — a person who is in a circle's people list and is nobody.
/// </para>
/// <para>
/// <strong>Cleanups run after the guards and before the account row is deleted</strong>, which is
/// the order that fails safely. The two contexts have separate <c>DbContext</c>s over one database,
/// so there is no single transaction spanning them, and one of the two orders has to be chosen with
/// its failure mode in mind:
/// </para>
/// <list type="bullet">
/// <item><strong>Cleanup first</strong> (this order): if the account delete then fails, a live
/// account has been removed from its circles. Bad, but it is bounded, it affects only the user who
/// just asked for exactly this, and they can be invited back.</item>
/// <item><strong>Account first:</strong> if the cleanup then fails, orphan rows are left in
/// <em>other people's</em> circles, permanently, with the account gone and no way to retry. That
/// corrupts data belonging to people who did not ask for anything.</item>
/// </list>
/// <para>
/// This is the same reasoning that already puts session revocation ahead of the delete: order the
/// steps so the surviving state is the recoverable one.
/// </para>
/// </remarks>
public interface IAccountDeletionCleanup
{
    /// <summary>Removes everything this context holds for a departing account.</summary>
    /// <param name="userId">The account being deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the context holds nothing for that account.</returns>
    Task CleanUpAsync(Guid userId, CancellationToken cancellationToken = default);
}
