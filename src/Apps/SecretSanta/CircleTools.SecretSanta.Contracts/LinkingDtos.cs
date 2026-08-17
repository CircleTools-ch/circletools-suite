namespace CircleTools.SecretSanta.Contracts;

// M4 — Linking. These contracts are deliberately Secret-Santa-shaped: they describe the one
// server-side surface ADR-0020 sanctioned (a per-user assignment projection) and the member-link
// variant of the M2 invitation machinery. The Main host references them for this surface only; the
// generic M3 sync store stays opaque and schema-ignorant (FEAT-005, ADR-0033).

/// <summary>
/// Organizer's request to link a group member to whoever owns an email address (FR-028). Routed
/// through the same constant-time account lookup as an M2 invitation, so the response reveals nothing
/// about whether the address has an account (NFR-005).
/// </summary>
/// <param name="GroupId">The group the member belongs to.</param>
/// <param name="GroupName">
/// The group's name, carried on the request because the server stores the group opaquely and cannot
/// read it — it is shown to the recipient in their pending list.
/// </param>
/// <param name="MemberId">The member to link.</param>
/// <param name="MemberDisplayName">The member's display name, shown to the recipient in their pending list.</param>
/// <param name="Email">The address to invite to claim the member.</param>
public sealed record SendMemberLinkRequest(
    Guid GroupId,
    string GroupName,
    Guid MemberId,
    string MemberDisplayName,
    string Email);

/// <summary>The outcome of a <see cref="SendMemberLinkRequest"/>.</summary>
public enum MemberLinkOutcome
{
    /// <summary>
    /// The request was recorded and, if the address has an account, an invitation was sent. Returned
    /// for <em>every</em> non-self request regardless of account existence, so decline and expiry stay
    /// indistinguishable and no address is probed (NFR-005).
    /// </summary>
    RequestSent = 0,

    /// <summary>
    /// The organizer linked their <em>own</em> account: the link is established immediately with no
    /// second step. Only ever returned when the address resolves to the caller (self-link), which the
    /// caller already knows, so it leaks nothing.
    /// </summary>
    SelfLinked = 1,

    /// <summary>
    /// Refused: the address resolves to an account that already backs a different member of this group —
    /// either an accepted link or a still-outstanding invitation. One account backs at most one member per
    /// group, so a duplicate is rejected at send time with immediate feedback instead of silently queued.
    /// <para>
    /// This is a deliberate, <em>scoped</em> relaxation of the constant-time NFR-005 guarantee: it only
    /// ever fires for accounts already linked to (or invited into) the caller's <strong>own</strong> group,
    /// whose linked members the caller can already see. An arbitrary address still returns
    /// <see cref="RequestSent"/>, so the surface cannot be used to probe for accounts (ADR-0044).
    /// </para>
    /// </summary>
    AlreadyLinkedInGroup = 2,
}

/// <summary>The response to a <see cref="SendMemberLinkRequest"/>.</summary>
/// <param name="Outcome">Whether the request was sent or auto-accepted as a self-link.</param>
public sealed record SendMemberLinkResponse(MemberLinkOutcome Outcome);

/// <summary>
/// Registers a member-link established by <strong>create-from-circle</strong> (FR-027): the organizer
/// and the target account share a circle, which is the consent (ADR-0018), so no invitation round-trip
/// is needed. The server verifies the shared-circle claim before recording the link.
/// </summary>
/// <param name="GroupId">The group the member belongs to.</param>
/// <param name="GroupName">The group name (the server stores the group opaquely).</param>
/// <param name="MemberId">The member being linked.</param>
/// <param name="MemberDisplayName">The member's display name.</param>
/// <param name="CircleId">The origin circle both accounts share — the server checks membership against it.</param>
/// <param name="TargetAccountId">The account to link (must be a member of <paramref name="CircleId"/>).</param>
public sealed record RegisterCircleLinkRequest(
    Guid GroupId,
    string GroupName,
    Guid MemberId,
    string MemberDisplayName,
    Guid CircleId,
    Guid TargetAccountId);

/// <summary>
/// Registers the organizer linking their <strong>own</strong> account to a member of a group they just
/// created (FR-032). Creating a group makes you a member of it, so — like the create-from-circle link —
/// this needs no invitation round-trip and no address: the caller and the target are the same verified
/// account, which the server takes from the bearer token rather than the request. The organizer's group
/// document carries the matching <c>Member.UserId</c>, applied from the link resolution on the next sync.
/// </summary>
/// <param name="GroupId">The group the member belongs to.</param>
/// <param name="GroupName">The group name (the server stores the group opaquely and cannot read it).</param>
/// <param name="MemberId">The member representing the organizer.</param>
/// <param name="MemberDisplayName">The member's display name.</param>
public sealed record SelfLinkRequest(
    Guid GroupId,
    string GroupName,
    Guid MemberId,
    string MemberDisplayName);

/// <summary>
/// A pending member-link request waiting for the signed-in caller, whose verified address it was sent
/// to (FR-028). Accepted or declined in the app, with no token from the mail (mirrors M2's
/// <c>JoinRequestResponse</c>). Carries nothing that leaks another member's data.
/// </summary>
/// <param name="Id">The member-link request to accept or decline.</param>
/// <param name="GroupId">The group being joined, so the caller's client can correlate the request.</param>
/// <param name="GroupName">The group the caller is invited to join as a member.</param>
/// <param name="MemberDisplayName">The display name of the member the caller would become.</param>
/// <param name="InviterDisplayName">The display name of the organizer who sent the request.</param>
public sealed record PendingMemberLinkResponse(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string MemberDisplayName,
    string InviterDisplayName);

/// <summary>
/// The outcome of accepting a member-link request. A managed member cannot be linked directly; the
/// caller is told to be promoted first (FR-019, ADR-0018).
/// </summary>
public enum AcceptMemberLinkOutcome
{
    /// <summary>The link was established; the caller now owns that membership's wishlist and assignment.</summary>
    Linked = 0,

    /// <summary>Refused: a different account already backs a member of that group, or backs this member.</summary>
    AlreadyLinked = 1,

    /// <summary>Refused: the target member is a managed member and must be promoted before it can be linked.</summary>
    ManagedMemberMustBePromoted = 2,
}

/// <summary>The response to accepting a member-link request.</summary>
/// <param name="Outcome">The result of the accept attempt.</param>
public sealed record AcceptMemberLinkResponse(AcceptMemberLinkOutcome Outcome);

/// <summary>
/// One link resolution the organizer's client applies to its group on the next sync cycle, then
/// acknowledges. This is how an accepted / declined / cascaded link reaches the authoritative group
/// document without a second poller: the organizer's client sets or clears <c>Member.UserId</c> and
/// syncs (FEAT-005).
/// </summary>
/// <param name="Id">The resolution's identity, used to acknowledge it once applied.</param>
/// <param name="GroupId">The group whose member is affected.</param>
/// <param name="MemberId">The member to link or unlink.</param>
/// <param name="Kind">Whether the member was linked or unlinked.</param>
/// <param name="UserId">The linked account when <paramref name="Kind"/> is <see cref="MemberLinkResolutionKind.Linked"/>; otherwise <see langword="null"/>.</param>
public sealed record MemberLinkResolution(
    Guid Id,
    Guid GroupId,
    Guid MemberId,
    MemberLinkResolutionKind Kind,
    Guid? UserId);

/// <summary>The direction of a <see cref="MemberLinkResolution"/>.</summary>
public enum MemberLinkResolutionKind
{
    /// <summary>A member became linked to an account (accept, or self-link).</summary>
    Linked = 0,

    /// <summary>A member became unlinked (decline is not a resolution; this is explicit unlink or the account-deletion cascade).</summary>
    Unlinked = 1,
}

/// <summary>Pending link resolutions for the organizer's groups, drained each sync cycle.</summary>
/// <param name="Resolutions">The resolutions to apply, oldest first.</param>
public sealed record MemberLinkResolutionsResponse(IReadOnlyList<MemberLinkResolution> Resolutions);

/// <summary>Acknowledges resolutions the organizer's client has applied to its group document.</summary>
/// <param name="ResolutionIds">The resolution ids to mark delivered.</param>
public sealed record AckMemberLinkResolutionsRequest(IReadOnlyList<Guid> ResolutionIds);

/// <summary>
/// The organizer publishes, for each <em>linked</em> member, the minimal assignment projection
/// ADR-0020 permits — giver account, recipient view, group — when a draw completes or is re-run. A
/// higher <paramref name="DrawVersion"/> replaces prior rows so a linked member never reads a stale
/// pairing (FR-029).
/// </summary>
/// <param name="GroupId">The group whose draw is being published.</param>
/// <param name="GroupName">The group name, shown to a linked member alongside their assignment.</param>
/// <param name="EventId">The event (occasion) whose draw is being published (ADR-0042).</param>
/// <param name="EventName">The event name, shown to a linked member alongside their assignment.</param>
/// <param name="DrawVersion">A monotonically increasing draw version; a re-run publishes a higher value.</param>
/// <param name="Budget">The gift budget, shown to the recipient's giver.</param>
/// <param name="Event">The event details, shown to the recipient's giver.</param>
/// <param name="Assignments">One row per linked giver.</param>
public sealed record PublishAssignmentsRequest(
    Guid GroupId,
    string GroupName,
    Guid EventId,
    string EventName,
    long DrawVersion,
    BudgetDto? Budget,
    EventDetailsDto? Event,
    IReadOnlyList<PublishedAssignment> Assignments);

/// <summary>
/// The organizer's event registry for a group (ADR-0042): the events a linked member may enumerate to
/// write a per-event wishlist before a draw. Published each sync; replaces the prior list wholesale.
/// </summary>
/// <param name="GroupName">The group name (the server stores the group opaquely).</param>
/// <param name="Events">The group's events, in order.</param>
public sealed record PublishGroupEventsRequest(string GroupName, IReadOnlyList<GroupEventEntry> Events);

/// <summary>One event in the registry the organizer publishes.</summary>
/// <param name="EventId">The event identity.</param>
/// <param name="Name">The event name (may be empty).</param>
/// <param name="Date">The optional event date, so a linked member can hide past occasions (FR-030).</param>
public sealed record GroupEventEntry(Guid EventId, string Name, DateTimeOffset? Date);

/// <summary>
/// One published pairing: the linked giver's account, and the recipient view they are allowed to see.
/// The wishlist snapshot is the organizer's copy at publish time; a linked recipient's own wishlist,
/// if present, overrides it at read time (FR-030).
/// </summary>
/// <param name="GiverUserId">The account of the linked giver who may read this row.</param>
/// <param name="RecipientMemberId">The recipient member's identity, used to compose their current wishlist at read time.</param>
/// <param name="RecipientName">The recipient's display name.</param>
/// <param name="RecipientWishlistSnapshot">The recipient's wishlist as the organizer holds it at publish time.</param>
public sealed record PublishedAssignment(
    Guid GiverUserId,
    Guid RecipientMemberId,
    string RecipientName,
    IReadOnlyList<string> RecipientWishlistSnapshot);

/// <summary>
/// The signed-in caller's <em>own</em> assignment in one group (FR-029). A request for anyone else's
/// is denied, never filtered to empty; a non-organizer never receives more than this one row.
/// </summary>
/// <param name="GroupId">The group the assignment belongs to.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="EventId">The event (occasion) the assignment belongs to (ADR-0042).</param>
/// <param name="EventName">The event name (may be empty).</param>
/// <param name="RecipientName">The caller's recipient.</param>
/// <param name="RecipientWishlist">The recipient's <em>current</em> wishlist (their own row if linked, else the published snapshot).</param>
/// <param name="Budget">The gift budget, or <see langword="null"/>.</param>
/// <param name="Event">The event details, or <see langword="null"/>.</param>
public sealed record MyAssignmentResponse(
    Guid GroupId,
    string GroupName,
    Guid EventId,
    string EventName,
    string RecipientName,
    IReadOnlyList<string> RecipientWishlist,
    BudgetDto? Budget,
    EventDetailsDto? Event);

/// <summary>The caller's assignments across every event they are linked in, each gated client-side.</summary>
/// <param name="Assignments">One row per drawn event the caller is a linked member of.</param>
public sealed record MyAssignmentsResponse(IReadOnlyList<MyAssignmentResponse> Assignments);

/// <summary>The linked member's own wishlist edit for one event of a membership (FR-030, ADR-0042). Empty items are ignored.</summary>
/// <param name="EventName">The event name, denormalized so the stored row needs no group read.</param>
/// <param name="Items">The wishlist items, in order.</param>
public sealed record UpdateMyWishlistRequest(string EventName, IReadOnlyList<string> Items);

/// <summary>One of the caller's self-owned wishlists (per event), so a new event can offer to copy a previous one (FR-030).</summary>
/// <param name="GroupId">The group the wishlist belongs to.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="EventId">The event within the group.</param>
/// <param name="EventName">The event name (may be empty).</param>
/// <param name="Items">The wishlist items, in order.</param>
public sealed record MyWishlistResponse(Guid GroupId, string GroupName, Guid EventId, string EventName, IReadOnlyList<string> Items);

/// <summary>The caller's wishlists across their memberships and events.</summary>
/// <param name="Wishlists">One row per (membership, event) with a self-owned wishlist.</param>
public sealed record MyWishlistsResponse(IReadOnlyList<MyWishlistResponse> Wishlists);

/// <summary>One (group, event) the signed-in caller is a linked member of, so they can own a wishlist there even before the draw.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="EventId">The event within the group.</param>
/// <param name="EventName">The event name (may be empty).</param>
/// <param name="EventDate">The optional event date, so past occasions can be hidden from the wishlist page (FR-030).</param>
public sealed record MyMembership(Guid GroupId, string GroupName, Guid EventId, string EventName, DateTimeOffset? EventDate);

/// <summary>The groups the caller is a linked member of.</summary>
/// <param name="Memberships">One row per linked membership.</param>
public sealed record MyMembershipsResponse(IReadOnlyList<MyMembership> Memberships);
