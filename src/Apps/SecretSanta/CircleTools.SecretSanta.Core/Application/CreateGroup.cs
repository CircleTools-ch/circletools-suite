using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to create a new group.</summary>
/// <param name="Name">A non-empty group name.</param>
public sealed record CreateGroupCommand(string Name) : ICommand<Guid>;

/// <summary>Handles <see cref="CreateGroupCommand"/> by creating and persisting a group.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class CreateGroupCommandHandler(IGroupRepository repository)
    : ICommandHandler<CreateGroupCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = Group.Create(command.Name);
        await repository.SaveAsync(group, cancellationToken);
        return group.Id;
    }
}
