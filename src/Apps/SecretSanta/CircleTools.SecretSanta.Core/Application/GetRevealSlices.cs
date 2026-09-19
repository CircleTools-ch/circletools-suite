using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Query returning one reveal-slice payload per giver for an event's current draw (ADR-0068, FEAT-020):
/// the decoded payloads a draw-capable client publishes so a backend-backed group's reveal links show
/// the current wishlist and event details. Distinct from <see cref="GetRevealLinksQuery"/> (which
/// encodes each payload into a URL) — both resolve the same payloads via <see cref="RevealPayloadFactory"/>.
/// </summary>
/// <param name="GroupId">The group whose reveal slices are requested.</param>
/// <param name="EventId">The event whose current draw the slices are built from.</param>
public sealed record GetRevealSlicesQuery(Guid GroupId, Guid EventId) : IQuery<IReadOnlyList<RevealSliceData>>;

/// <summary>Handles <see cref="GetRevealSlicesQuery"/> by resolving each giver's reveal payload.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class GetRevealSlicesQueryHandler(IGroupRepository repository)
    : IQueryHandler<GetRevealSlicesQuery, IReadOnlyList<RevealSliceData>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RevealSliceData>> HandleAsync(GetRevealSlicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        var occasion = group?.FindEvent(query.EventId);
        if (group is null || occasion is null)
        {
            return [];
        }

        var built = RevealPayloadFactory.Build(group, occasion);
        return built is null ? [] : [.. built.Select(b => new RevealSliceData(b.GiverMemberId, b.Payload))];
    }
}
