namespace AppSuite.Main.Service.Core.Domain;

/// <summary>
/// A rule that can block account deletion (FR-015).
/// </summary>
/// <remarks>
/// <para>
/// <strong>No guards are registered in M1</strong>, and that is deliberate rather than an
/// oversight. The rule that actually needs one — a sole circle owner must transfer ownership or
/// delete the circle first — cannot exist until circles do (M2). Shipping the extension point now
/// means M2 adds a guard instead of reopening the deletion path, which is the part that is easy to
/// get wrong under pressure.
/// </para>
/// <para>
/// [ADR-0022] settled that deletion is immediate and irreversible, so anything that must be
/// checked has to be checked <em>before</em> it starts.
/// </para>
/// </remarks>
public interface IAccountDeletionGuard
{
    /// <summary>Checks whether an account may be deleted.</summary>
    /// <param name="userId">The account being deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>null</c> when deletion may proceed, otherwise a reason the caller can show the user.
    /// </returns>
    Task<string?> CheckAsync(Guid userId, CancellationToken cancellationToken = default);
}
