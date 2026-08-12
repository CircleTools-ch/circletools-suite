using AppSuite.Main.Service.Core.Domain;

namespace AppSuite.Main.Service;

/// <summary>
/// The linking context's half of account deletion (FR-015). A deleted account that was a linked
/// member must not leave a dangling <c>UserId</c> in someone else's group: the cleanup unlinks every
/// referencing member — emitting an unlink resolution the organizer's client applies — and removes the
/// account's own assignment and wishlist rows. The member, its name and its wishlist survive and the
/// group opens without error.
/// </summary>
/// <remarks>
/// Registered as a second <see cref="IAccountDeletionCleanup"/> alongside Circles': the account
/// deletion flow resolves the whole set and runs each. It follows the same ordering rule — cleanup
/// before the account row — for the same reason (a recoverable failure beats an unrecoverable one).
/// </remarks>
/// <param name="store">The linking store.</param>
public sealed class LinkingAccountCleanup(ILinkingStore store) : IAccountDeletionCleanup
{
    /// <inheritdoc />
    public Task CleanUpAsync(Guid userId, CancellationToken cancellationToken = default) =>
        store.CleanUpAccountAsync(userId, cancellationToken);
}
