// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Query returning the full detail of a single group, or <see langword="null"/> if not found.</summary>
/// <param name="GroupId">The group to load.</param>
public sealed record GetGroupQuery(Guid GroupId) : IQuery<GroupDto?>;

/// <summary>Handles <see cref="GetGroupQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="time">The clock, so a past occasion's stale draw is not flagged "re-draw needed" (ADR-0052).</param>
public sealed class GetGroupQueryHandler(IGroupRepository repository, TimeProvider time)
    : IQueryHandler<GetGroupQuery, GroupDto?>
{
    /// <inheritdoc />
    public async Task<GroupDto?> HandleAsync(GetGroupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        return group is null ? null : GroupMapper.ToDto(group, time.GetLocalNow());
    }
}
