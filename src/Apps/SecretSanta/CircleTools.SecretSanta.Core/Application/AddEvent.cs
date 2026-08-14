using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to add a new event (occasion) to a group (ADR-0042).</summary>
/// <param name="GroupId">The group to add the event to.</param>
/// <param name="Name">The event name; may be empty.</param>
public sealed record AddEventCommand(Guid GroupId, string Name) : ICommand<Guid>;

/// <summary>Handles <see cref="AddEventCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class AddEventCommandHandler(IGroupRepository repository)
    : ICommandHandler<AddEventCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(AddEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var eventId = group.AddEvent(command.Name);
        await repository.SaveAsync(group, cancellationToken);
        return eventId;
    }
}
