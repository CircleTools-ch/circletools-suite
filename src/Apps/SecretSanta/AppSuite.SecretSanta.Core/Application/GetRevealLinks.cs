using System.Globalization;
using AppSuite.Application;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Reveal;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Query returning one reveal link per member for the group's current draw.</summary>
/// <param name="GroupId">The group whose reveal links are requested.</param>
public sealed record GetRevealLinksQuery(Guid GroupId) : IQuery<IReadOnlyList<RevealLinkDto>>;

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
        if (group?.CurrentDraw is null)
        {
            return [];
        }

        var links = new List<RevealLinkDto>(group.CurrentDraw.Assignments.Count);
        foreach (var assignment in group.CurrentDraw.Assignments)
        {
            var giver = group.FindMember(assignment.GiverMemberId);
            var recipient = group.FindMember(assignment.RecipientMemberId);
            if (giver is null || recipient is null)
            {
                continue;
            }

            var payload = new RevealPayload(
                RevealPayload.CurrentVersion,
                recipient.Name,
                recipient.Wishlist.Items,
                group.Budget?.Amount,
                group.Budget?.Currency,
                group.EventDetails?.Date?.ToString("O", CultureInfo.InvariantCulture),
                group.EventDetails?.Location,
                group.EventDetails?.Description);

            links.Add(linkBuilder.Build(giver.Name, payload));
        }

        return links;
    }
}
