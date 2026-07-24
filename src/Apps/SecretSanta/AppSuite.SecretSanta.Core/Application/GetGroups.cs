using AppSuite.Application;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Query returning a summary of every group.</summary>
public sealed record GetGroupsQuery : IQuery<IReadOnlyList<GroupSummaryDto>>;

/// <summary>Handles <see cref="GetGroupsQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class GetGroupsQueryHandler(IGroupRepository repository)
    : IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupSummaryDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupSummaryDto>> HandleAsync(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var groups = await repository.ListAsync(cancellationToken);
        return groups.Select(GroupMapper.ToSummary).ToList();
    }
}
