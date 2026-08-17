using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to rename an event.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="EventId">The event to rename.</param>
/// <param name="Name">The new name; may be empty.</param>
public sealed record RenameEventCommand(Guid GroupId, Guid EventId, string Name) : ICommand;

/// <summary>Handles <see cref="RenameEventCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class RenameEventCommandHandler(IGroupRepository repository)
    : ICommandHandler<RenameEventCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(RenameEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.RenameEvent(command.EventId, command.Name);
        await repository.SaveAsync(group, cancellationToken);
    }
}
