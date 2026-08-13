using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to remove a directed exclusion between two members.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
public sealed record RemoveExclusionCommand(Guid GroupId, Guid GiverMemberId, Guid RecipientMemberId) : ICommand;

/// <summary>Handles <see cref="RemoveExclusionCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class RemoveExclusionCommandHandler(IGroupRepository repository)
    : ICommandHandler<RemoveExclusionCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(RemoveExclusionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.RemoveExclusion(command.GiverMemberId, command.RecipientMemberId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
