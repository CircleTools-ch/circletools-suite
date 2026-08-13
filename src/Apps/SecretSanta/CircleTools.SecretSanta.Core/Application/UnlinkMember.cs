using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Command applied by the organizer's client when an unlink resolution arrives on the sync cycle —
/// an explicit unlink, or the graceful unlink cascaded from a linked account's deletion (FR-015).
/// The member, its name and its wishlist survive; only the account link is cleared. Unlinking does
/// not invalidate a completed draw.
/// </summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member to unlink.</param>
public sealed record UnlinkMemberCommand(Guid GroupId, Guid MemberId) : ICommand;

/// <summary>Handles <see cref="UnlinkMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class UnlinkMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<UnlinkMemberCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(UnlinkMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.UnlinkMember(command.MemberId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
