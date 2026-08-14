using System.Globalization;
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
        if (group is null || occasion?.CurrentDraw is null)
        {
            return [];
        }

        var links = new List<RevealLinkDto>(occasion.CurrentDraw.Assignments.Count);
        foreach (var assignment in occasion.CurrentDraw.Assignments)
        {
            var giver = group.FindMember(assignment.GiverMemberId);
            var recipient = group.FindMember(assignment.RecipientMemberId);
            if (giver is null || recipient is null)
            {
                // The draw no longer matches the member set. Skipping the broken pairs would hand
                // the organizer a plausible-looking but incomplete set of links, silently dropping
                // whoever is missing from the exchange. Membership changes void every event's draw
                // (Group.RemoveMember), so this is a guard rather than an expected path.
                return [];
            }

            var payload = new RevealPayload(
                RevealPayload.CurrentVersion,
                recipient.Name,
                occasion.WishlistFor(recipient.Id).Items,
                occasion.Budget?.Amount,
                occasion.Budget?.Currency,
                occasion.EventDetails?.Date?.ToString("O", CultureInfo.InvariantCulture),
                occasion.EventDetails?.Location,
                occasion.EventDetails?.Description);

            links.Add(linkBuilder.Build(giver.Name, payload));
        }

        return links;
    }
}
