using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to set (or clear, when empty) a member's wishlist for one event (ADR-0042).</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="EventId">The event the wishlist belongs to.</param>
/// <param name="MemberId">The member whose wishlist it is.</param>
/// <param name="Items">The wishlist items; empty entries are dropped, and an empty list clears the wishlist.</param>
public sealed record SetMemberWishlistCommand(Guid GroupId, Guid EventId, Guid MemberId, IReadOnlyList<string> Items) : ICommand;

/// <summary>Handles <see cref="SetMemberWishlistCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class SetMemberWishlistCommandHandler(IGroupRepository repository)
    : ICommandHandler<SetMemberWishlistCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(SetMemberWishlistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.SetMemberWishlist(command.EventId, command.MemberId, Wishlist.Create(command.Items));
        await repository.SaveAsync(group, cancellationToken);
    }
}
