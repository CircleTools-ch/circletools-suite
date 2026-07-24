using AppSuite.Application;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Command to edit a member's name and wishlist.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member to edit.</param>
/// <param name="Name">The new, non-empty, group-unique name.</param>
/// <param name="Wishlist">The new wishlist items.</param>
public sealed record EditMemberCommand(Guid GroupId, Guid MemberId, string Name, IReadOnlyList<string> Wishlist) : ICommand;

/// <summary>Handles <see cref="EditMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class EditMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<EditMemberCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(EditMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.EditMember(command.MemberId, command.Name, Wishlist.Create(command.Wishlist));
        await repository.SaveAsync(group, cancellationToken);
    }
}
