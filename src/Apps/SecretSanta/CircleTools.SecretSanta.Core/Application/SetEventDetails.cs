using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to set or clear a group's event details.</summary>
/// <param name="GroupId">The group to update.</param>
/// <param name="Date">The optional event date.</param>
/// <param name="Location">The event location (may be empty).</param>
/// <param name="Description">The event description (may be empty).</param>
public sealed record SetEventDetailsCommand(Guid GroupId, DateTimeOffset? Date, string? Location, string? Description) : ICommand;

/// <summary>Handles <see cref="SetEventDetailsCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class SetEventDetailsCommandHandler(IGroupRepository repository)
    : ICommandHandler<SetEventDetailsCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(SetEventDetailsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var hasValue = command.Date is not null
            || !string.IsNullOrWhiteSpace(command.Location)
            || !string.IsNullOrWhiteSpace(command.Description);
        group.SetEventDetails(hasValue ? EventDetails.Create(command.Date, command.Location, command.Description) : null);
        await repository.SaveAsync(group, cancellationToken);
    }
}
