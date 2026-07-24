using AppSuite.Application;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Command to add a directed exclusion between two members.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The forbidden recipient member identity.</param>
public sealed record AddExclusionCommand(Guid GroupId, Guid GiverMemberId, Guid RecipientMemberId) : ICommand;

/// <summary>Handles <see cref="AddExclusionCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class AddExclusionCommandHandler(IGroupRepository repository)
    : ICommandHandler<AddExclusionCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(AddExclusionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.AddExclusion(command.GiverMemberId, command.RecipientMemberId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
