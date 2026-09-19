using System.Globalization;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Builds the per-giver <see cref="RevealPayload"/>s for an event's current draw — the single place that
/// resolves each assignment into the recipient's name, wishlist, gift note, budget and event details.
/// Shared by <see cref="GetRevealLinksQueryHandler"/> (which encodes each payload into a link) and
/// <see cref="GetRevealSlicesQueryHandler"/> (which publishes each as a live reveal slice, ADR-0068), so
/// the two never drift.
/// </summary>
public static class RevealPayloadFactory
{
    /// <summary>One giver's resolved reveal payload.</summary>
    /// <param name="GiverMemberId">The giver.</param>
    /// <param name="GiverName">The giver's display name (the reveal link's readable "for" label).</param>
    /// <param name="Payload">The reveal payload for that giver.</param>
    public readonly record struct Built(Guid GiverMemberId, string GiverName, RevealPayload Payload);

    /// <summary>
    /// Resolves every giver's reveal payload for <paramref name="occasion"/>'s current draw, or
    /// <see langword="null"/> when there is no draw or the draw no longer matches the roster (a broken
    /// pair) — the same guard both callers already apply, kept in one place.
    /// </summary>
    /// <param name="group">The owning group (resolves member names, wishlists, gift notes).</param>
    /// <param name="occasion">The event whose current draw is resolved.</param>
    /// <returns>The per-giver payloads, or <see langword="null"/> when there is nothing to reveal.</returns>
    public static IReadOnlyList<Built>? Build(Group group, Event occasion)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(occasion);

        if (occasion.CurrentDraw is null)
        {
            return null;
        }

        var built = new List<Built>(occasion.CurrentDraw.Assignments.Count);
        foreach (var assignment in occasion.CurrentDraw.Assignments)
        {
            var giver = group.FindMember(assignment.GiverMemberId);
            var recipient = group.FindMember(assignment.RecipientMemberId);
            if (giver is null || recipient is null)
            {
                // The draw no longer matches the member set — a guard, not an expected path (a
                // membership change voids the draw). Hand back nothing rather than a partial set.
                return null;
            }

            var payload = new RevealPayload(
                RevealPayload.CurrentVersion,
                recipient.Name,
                occasion.WishlistFor(recipient.Id).Items,
                occasion.Budget?.Amount,
                occasion.Budget?.Currency,
                occasion.EventDetails?.Date?.ToString("O", CultureInfo.InvariantCulture),
                occasion.EventDetails?.Location,
                occasion.EventDetails?.Description,
                occasion.Budget?.MinAmount,
                string.IsNullOrEmpty(recipient.GiftNote) ? null : recipient.GiftNote);

            built.Add(new Built(giver.Id, giver.Name, payload));
        }

        return built;
    }
}
