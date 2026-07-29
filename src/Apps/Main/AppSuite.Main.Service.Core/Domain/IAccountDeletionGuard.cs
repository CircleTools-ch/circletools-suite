namespace AppSuite.Main.Service.Core.Domain;

/// <summary>
/// A rule that can block account deletion (FR-015).
/// </summary>
/// <remarks>
/// <para>
/// <strong>M1 registered no guards</strong>, and that was deliberate rather than an oversight. The
/// rule that actually needs one — an owner must hand a circle over or delete it first — cannot exist
/// until circles do (M2). Shipping the extension point early meant M2 added
/// <c>SoleOwnerCircleGuard</c> instead of reopening the deletion path, which is the part that is easy
/// to get wrong under pressure.
/// </para>
/// <para>
/// [ADR-0022] settled that deletion is immediate and irreversible, so anything that must be
/// checked has to be checked <em>before</em> it starts. A guard therefore only answers; it never
/// changes anything. Cleaning up after a deletion that <em>is</em> allowed is the other half, and
/// belongs to <see cref="IAccountDeletionCleanup"/>.
/// </para>
/// </remarks>
public interface IAccountDeletionGuard
{
    /// <summary>Checks whether an account may be deleted.</summary>
    /// <param name="userId">The account being deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>null</c> when deletion may proceed, otherwise the refusal — a translatable code and the
    /// named blockers the user has to clear first.
    /// </returns>
    Task<AccountDeletionRefusal?> CheckAsync(Guid userId, CancellationToken cancellationToken = default);
}
