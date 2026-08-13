using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Command applied by the <em>organizer's</em> client when a member-link resolution arrives on the
/// sync cycle: it writes the authoritative <c>Member.UserId</c> onto the group so the group document
/// stays the single source of truth for the roster. Linking does not invalidate a completed draw.
/// </summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member to link.</param>
/// <param name="UserId">The account the member is now linked to.</param>
public sealed record LinkMemberCommand(Guid GroupId, Guid MemberId, Guid UserId) : ICommand;

/// <summary>Handles <see cref="LinkMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class LinkMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<LinkMemberCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(LinkMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.LinkMember(command.MemberId, command.UserId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
