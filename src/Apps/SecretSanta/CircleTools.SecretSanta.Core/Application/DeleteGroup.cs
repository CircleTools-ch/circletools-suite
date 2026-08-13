using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to delete a group.</summary>
/// <param name="GroupId">The group to delete.</param>
public sealed record DeleteGroupCommand(Guid GroupId) : ICommand;

/// <summary>Handles <see cref="DeleteGroupCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class DeleteGroupCommandHandler(IGroupRepository repository)
    : ICommandHandler<DeleteGroupCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await repository.DeleteAsync(command.GroupId, cancellationToken);
    }
}
