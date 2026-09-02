using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Query returning a summary of every group.</summary>
public sealed record GetGroupsQuery : IQuery<IReadOnlyList<GroupSummaryDto>>;

/// <summary>Handles <see cref="GetGroupsQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="time">The clock, so a past occasion's stale draw is not flagged "re-draw needed" (ADR-0052).</param>
public sealed class GetGroupsQueryHandler(IGroupRepository repository, TimeProvider time)
    : IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupSummaryDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupSummaryDto>> HandleAsync(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var now = time.GetLocalNow();
        var groups = await repository.ListAsync(cancellationToken);
        return groups.Select(g => GroupMapper.ToSummary(g, now)).ToList();
    }
}
