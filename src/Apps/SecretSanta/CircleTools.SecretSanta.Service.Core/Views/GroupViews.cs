using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.Service.Core.Views;

/// <summary>
/// The views the server derives from an online group's document on read (ADR-0086). Nothing here is ever
/// published by a device, so a view is as current as the document. Each view holds at most one pairing — the
/// reader's own (NFR-002, ADR-0020).
/// </summary>
public static class GroupViews
{
    /// <summary>What a participant sees: their own pairing per event, their recipient's wishes, their own entry.</summary>
    /// <param name="document">The group document, with member entries already merged.</param>
    /// <param name="version">The document version.</param>
    /// <param name="memberId">The participant's member.</param>
    /// <param name="permissions">The caller's permission names.</param>
    /// <returns>The view, or <see langword="null"/> when the member isn't on the roster any more.</returns>
    public static ParticipantView? ForParticipant(
        GroupState document,
        long version,
        Guid memberId,
        IReadOnlyList<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(permissions);

        var group = Group.FromState(document);
        var member = group.FindMember(memberId);
        if (member is null || member.IsArchived)
        {
            return null;
        }

        var events = group.Events
            .Select(occasion => new ParticipantEventView(
                occasion.Id,
                occasion.Name,
                occasion.EventDetails?.Date,
                occasion.WishlistFor(memberId).Items,
                OwnPairing(group, occasion, memberId)))
            .ToList();

        return new ParticipantView(
            group.Id,
            group.Name,
            version,
            member.Id,
            member.Name,
            string.IsNullOrEmpty(member.GiftNote) ? null : member.GiftNote,
            events,
            permissions);
    }

    /// <summary>The reveal page for one giver in one event (ADR-0068/0082, derived per ADR-0086).</summary>
    /// <param name="document">The group document, with member entries already merged.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="giverMemberId">The giver the reveal link was made for.</param>
    /// <returns>The page, or <see langword="null"/> when the event, the draw or the giver is gone.</returns>
    public static RevealPayload? ForReveal(GroupState document, Guid eventId, Guid giverMemberId)
    {
        ArgumentNullException.ThrowIfNull(document);

        var group = Group.FromState(document);
        var occasion = group.FindEvent(eventId);
        return occasion is null ? null : OwnPairing(group, occasion, giverMemberId);
    }

    /// <summary>What the join link shows: the active roster and who is already taken.</summary>
    /// <param name="document">The group document.</param>
    /// <param name="takenMemberIds">Members that already have a principal.</param>
    /// <returns>The preview.</returns>
    public static JoinPreview ForJoin(GroupState document, IReadOnlySet<Guid> takenMemberIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(takenMemberIds);

        var group = Group.FromState(document);
        return new JoinPreview(
            group.Id,
            group.Name,
            [.. group.Members.Select(m => new JoinableMember(m.Id, m.Name, takenMemberIds.Contains(m.Id)))]);
    }

    // Only the reader's own pairing is ever built into a view: the factory builds the event's set, and everything
    // but this giver's payload is dropped here, before it can reach a response.
    private static RevealPayload? OwnPairing(Group group, Event occasion, Guid giverMemberId) =>
        RevealPayloadFactory.Build(group, occasion)?
            .Where(built => built.GiverMemberId == giverMemberId)
            .Select(built => built.Payload)
            .SingleOrDefault();
}
