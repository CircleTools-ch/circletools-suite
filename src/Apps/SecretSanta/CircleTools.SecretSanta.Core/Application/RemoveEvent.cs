using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to remove an event and everything it owns (its draws and wishlists).</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="EventId">The event to remove.</param>
public sealed record RemoveEventCommand(Guid GroupId, Guid EventId) : ICommand;

/// <summary>Handles <see cref="RemoveEventCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class RemoveEventCommandHandler(IGroupRepository repository)
    : ICommandHandler<RemoveEventCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(RemoveEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.RemoveEvent(command.EventId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
