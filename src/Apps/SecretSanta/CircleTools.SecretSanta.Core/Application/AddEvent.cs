// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to add a new event (occasion) to a group (ADR-0042).</summary>
/// <param name="GroupId">The group to add the event to.</param>
/// <param name="Name">The event name; may be empty.</param>
/// <param name="CarryForwardFromEventId">
/// When set, carry forward from this existing event (FR-035): copy each current member's wishlist and the
/// budget, and seed the new event's no-repeat baseline with that event's draw. <see langword="null"/>
/// (the default) creates a blank event, exactly as before.
/// </param>
public sealed record AddEventCommand(Guid GroupId, string Name, Guid? CarryForwardFromEventId = null) : ICommand<Guid>;

/// <summary>Handles <see cref="AddEventCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="timeProvider">The clock used to stamp a carried-forward draw baseline.</param>
public sealed class AddEventCommandHandler(IGroupRepository repository, TimeProvider timeProvider)
    : ICommandHandler<AddEventCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(AddEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var eventId = command.CarryForwardFromEventId is { } sourceId
            ? group.AddEventFrom(command.Name, sourceId, timeProvider.GetUtcNow())
            : group.AddEvent(command.Name);
        await repository.SaveAsync(group, cancellationToken);
        return eventId;
    }
}
