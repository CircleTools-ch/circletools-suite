using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service;

/// <summary>
/// The shared-group context's half of account deletion (FR-043, ADR-0058). A deleted account that
/// owned shared-by-link groups must not strand the people who joined them: the default disposition
/// hands each owned group back to shared-by-link (clears ownership), so it stays online and its
/// join-link participants keep access. Local copies on any device are untouched.
/// </summary>
/// <remarks>
/// Registered as another <see cref="IAccountDeletionCleanup"/>; the delete flow resolves the whole set
/// and runs each before the account row is removed (the recoverable-failure order).
/// </remarks>
/// <param name="store">The shared-group store.</param>
public sealed class SharedGroupAccountCleanup(ISharedGroupStore store) : IAccountDeletionCleanup
{
    /// <inheritdoc />
    public Task CleanUpAsync(Guid userId, CancellationToken cancellationToken = default) =>
        store.ReleaseAllOwnedAsync(userId, cancellationToken);
}
