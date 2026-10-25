namespace CircleTools.SecretSanta.Contracts;

/// <summary>
/// What a participant of an online group sees (ADR-0086): only their own pairing per event and their recipient's
/// wishes — never another pairing (NFR-002) — plus their own entry so they can edit it.
/// </summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="Version">The document version the view was derived from.</param>
/// <param name="MemberId">The participant's member.</param>
/// <param name="MemberName">The participant's member name.</param>
/// <param name="GiftNote">The participant's own gift note, if any.</param>
/// <param name="Events">One entry per event of the group.</param>
/// <param name="Permissions">What the caller may do here (camel-cased permission names).</param>
public sealed record ParticipantView(
    Guid GroupId,
    string GroupName,
    long Version,
    Guid MemberId,
    string MemberName,
    string? GiftNote,
    IReadOnlyList<ParticipantEventView> Events,
    IReadOnlyList<string> Permissions);

/// <summary>One event as a participant sees it.</summary>
/// <param name="EventId">The event.</param>
/// <param name="EventName">The event's name.</param>
/// <param name="Date">The event's date, if set.</param>
/// <param name="Wishlist">The participant's own wishlist for this event.</param>
/// <param name="Assignment">
/// The participant's own pairing for this event — whom they give to and that person's wishes — or
/// <see langword="null"/> before the draw.
/// </param>
public sealed record ParticipantEventView(
    Guid EventId,
    string EventName,
    DateTimeOffset? Date,
    IReadOnlyList<string> Wishlist,
    RevealPayload? Assignment);

/// <summary>A member of an online group as the join link shows it: who can still be picked (ADR-0086).</summary>
/// <param name="MemberId">The member.</param>
/// <param name="Name">The member's name.</param>
/// <param name="Taken">Whether someone already took part as this member.</param>
public sealed record JoinableMember(Guid MemberId, string Name, bool Taken);

/// <summary>What the join link shows before anyone picks themselves.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="Members">The roster, with who is already taken.</param>
public sealed record JoinPreview(Guid GroupId, string GroupName, IReadOnlyList<JoinableMember> Members);
