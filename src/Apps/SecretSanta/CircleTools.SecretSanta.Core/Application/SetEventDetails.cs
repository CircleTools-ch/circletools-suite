// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to set or clear one event's details (date, location, description).</summary>
/// <param name="GroupId">The group to update.</param>
/// <param name="EventId">The event whose details are set.</param>
/// <param name="Date">The optional event date.</param>
/// <param name="Location">The event location (may be empty).</param>
/// <param name="Description">The event description (may be empty).</param>
public sealed record SetEventDetailsCommand(Guid GroupId, Guid EventId, DateTimeOffset? Date, string? Location, string? Description) : ICommand;

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
        group.SetEventDetails(command.EventId, hasValue ? EventDetails.Create(command.Date, command.Location, command.Description) : null);
        await repository.SaveAsync(group, cancellationToken);
    }
}
