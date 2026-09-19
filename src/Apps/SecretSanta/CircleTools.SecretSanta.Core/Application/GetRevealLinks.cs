using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Reveal;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Query returning one reveal link per member for one event's current draw.</summary>
/// <param name="GroupId">The group whose reveal links are requested.</param>
/// <param name="EventId">The event whose current draw the links are built from.</param>
public sealed record GetRevealLinksQuery(Guid GroupId, Guid EventId) : IQuery<IReadOnlyList<RevealLinkDto>>;

/// <summary>Handles <see cref="GetRevealLinksQuery"/> by encoding each assignment into a reveal link.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="linkBuilder">The reveal-link builder.</param>
public sealed class GetRevealLinksQueryHandler(IGroupRepository repository, RevealLinkBuilder linkBuilder)
    : IQueryHandler<GetRevealLinksQuery, IReadOnlyList<RevealLinkDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RevealLinkDto>> HandleAsync(GetRevealLinksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        var occasion = group?.FindEvent(query.EventId);
        if (group is null || occasion is null)
        {
            return [];
        }

        // A stale draw (roster changed since it ran, ADR-0052) still yields links: an archived member's
        // name resolves through FindMember, so a past occasion's links stay reachable — that is the whole
        // point of keeping the draw. The UI warns that the draw no longer matches the roster; it is not
        // blocked here. RevealPayloadFactory returns null for no-draw or a genuinely broken (legacy)
        // draw, which maps to an empty set.
        var built = RevealPayloadFactory.Build(group, occasion);
        if (built is null)
        {
            return [];
        }

        return [.. built.Select(b => linkBuilder.Build(b.GiverName, b.GiverMemberId, b.Payload))];
    }
}
