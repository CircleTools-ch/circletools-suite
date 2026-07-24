using AppSuite.Application;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Query returning the full detail of a single group, or <see langword="null"/> if not found.</summary>
/// <param name="GroupId">The group to load.</param>
public sealed record GetGroupQuery(Guid GroupId) : IQuery<GroupDto?>;

/// <summary>Handles <see cref="GetGroupQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class GetGroupQueryHandler(IGroupRepository repository)
    : IQueryHandler<GetGroupQuery, GroupDto?>
{
    /// <inheritdoc />
    public async Task<GroupDto?> HandleAsync(GetGroupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        return group is null ? null : GroupMapper.ToDto(group);
    }
}
