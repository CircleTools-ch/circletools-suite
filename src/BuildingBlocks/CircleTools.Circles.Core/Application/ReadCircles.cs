using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Application;

/// <summary>Query for the circles the caller belongs to (FR-016).</summary>
/// <param name="UserId">The caller.</param>
public sealed record GetMyCirclesQuery(Guid UserId) : IQuery<IReadOnlyList<CircleSummaryResponse>>;

/// <summary>Handles <see cref="GetMyCirclesQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class GetMyCirclesQueryHandler(ICircleRepository repository)
    : IQueryHandler<GetMyCirclesQuery, IReadOnlyList<CircleSummaryResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CircleSummaryResponse>> HandleAsync(
        GetMyCirclesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var circles = await repository.ListForUserAsync(query.UserId, cancellationToken);

        return [.. circles.Select(c => CircleMapper.ToSummary(c, query.UserId))];
    }
}

/// <summary>Query for one circle and its people (FR-016, FR-020).</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
public sealed record GetCircleQuery(Guid UserId, Guid CircleId) : IQuery<CircleResult<CircleDetailResponse>>;

/// <summary>Handles <see cref="GetCircleQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <remarks>
/// A circle that does not exist and a circle the caller is not in both come back as
/// <see cref="CircleError.NotAMember"/>. Collapsing them here, rather than at each endpoint, is what
/// makes the two answers identical by construction (NFR-006).
/// </remarks>
public sealed class GetCircleQueryHandler(ICircleRepository repository)
    : IQueryHandler<GetCircleQuery, CircleResult<CircleDetailResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CircleDetailResponse>> HandleAsync(
        GetCircleQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var circle = await repository.FindAsync(query.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResults.Refused<CircleDetailResponse>(CircleError.NotAMember);
        }

        var permission = circle.Authorize(query.UserId, CircleRole.Member);
        if (!permission.IsSuccess)
        {
            return CircleResults.Refused<CircleDetailResponse>(permission.Error!.Value);
        }

        return CircleResults.Ok(CircleMapper.ToDetail(circle, query.UserId));
    }
}

/// <summary>
/// Query for the circles the caller owns alone — what FR-015's deletion guard and FR-021's leave
/// refusal both need in order to offer a way out rather than a dead end.
/// </summary>
/// <param name="UserId">The caller.</param>
public sealed record GetSoleOwnedCirclesQuery(Guid UserId) : IQuery<IReadOnlyList<SoleOwnedCircleResponse>>;

/// <summary>Handles <see cref="GetSoleOwnedCirclesQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class GetSoleOwnedCirclesQueryHandler(ICircleRepository repository)
    : IQueryHandler<GetSoleOwnedCirclesQuery, IReadOnlyList<SoleOwnedCircleResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SoleOwnedCircleResponse>> HandleAsync(
        GetSoleOwnedCirclesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var owned = await repository.ListOwnedByAsync(query.UserId, cancellationToken);

        return [.. owned.Select(c => CircleMapper.ToSoleOwned(c, query.UserId))];
    }
}
