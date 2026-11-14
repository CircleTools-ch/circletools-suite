// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Query returning the full detail of one event, or <see langword="null"/> if the group or event is not found.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="EventId">The event to load.</param>
public sealed record GetEventQuery(Guid GroupId, Guid EventId) : IQuery<EventDto?>;

/// <summary>Handles <see cref="GetEventQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="time">The clock, so a past occasion's stale draw is not flagged "re-draw needed" (ADR-0052).</param>
public sealed class GetEventQueryHandler(IGroupRepository repository, TimeProvider time)
    : IQueryHandler<GetEventQuery, EventDto?>
{
    /// <inheritdoc />
    public async Task<EventDto?> HandleAsync(GetEventQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        var occasion = group?.FindEvent(query.EventId);
        return group is null || occasion is null ? null : GroupMapper.ToEventDto(group, occasion, time.GetLocalNow());
    }
}
