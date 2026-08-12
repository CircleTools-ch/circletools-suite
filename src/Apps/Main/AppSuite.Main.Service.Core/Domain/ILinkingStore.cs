namespace AppSuite.Main.Service.Core.Domain;

/// <summary>The lifecycle state of a member-link request (M4, FR-028).</summary>
public enum MemberLinkState
{
    /// <summary>Sent and awaiting the recipient's decision. Indistinguishable from expired to the organizer.</summary>
    Pending = 0,

    /// <summary>Accepted (or self-linked): the member is linked to the target account.</summary>
    Linked = 1,

    /// <summary>Declined or revoked. Kept only as a terminal record; the organizer cannot tell it from expiry.</summary>
    Closed = 2,
}

/// <summary>A member-link request as the linking store keeps it. Server mechanism, not Secret-Santa domain language.</summary>
/// <param name="Id">The request identity.</param>
/// <param name="GroupId">The group the member belongs to.</param>
/// <param name="GroupName">The group name, stored denormalized because the group document is opaque to the server.</param>
/// <param name="MemberId">The member being linked.</param>
/// <param name="MemberDisplayName">The member's display name.</param>
/// <param name="OrganizerAccountId">The organizer who sent the request (and owns the group).</param>
/// <param name="TargetAccountId">The account the invited address resolved to, or <see langword="null"/> when it had none.</param>
/// <param name="State">The request's lifecycle state.</param>
public sealed record MemberLinkRecord(
    Guid Id,
    Guid GroupId,
    string GroupName,
    Guid MemberId,
    string MemberDisplayName,
    Guid OrganizerAccountId,
    Guid? TargetAccountId,
    MemberLinkState State);

/// <summary>A pending member-link for the signed-in recipient (target account), shown so they can accept or decline.</summary>
/// <param name="Id">The request identity.</param>
/// <param name="GroupId">The group being joined.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="MemberDisplayName">The member the recipient would become.</param>
public sealed record PendingMemberLink(Guid Id, Guid GroupId, string GroupName, string MemberDisplayName);

/// <summary>The direction of an applied link resolution the organizer's client drains.</summary>
public enum LinkResolutionKind
{
    /// <summary>The member became linked (accept, or self-link).</summary>
    Linked = 0,

    /// <summary>The member became unlinked (explicit unlink, or the account-deletion cascade).</summary>
    Unlinked = 1,
}

/// <summary>One resolution the organizer's client applies to its group document, then acknowledges.</summary>
/// <param name="Id">The resolution identity.</param>
/// <param name="GroupId">The affected group.</param>
/// <param name="MemberId">The affected member.</param>
/// <param name="Kind">Whether the member was linked or unlinked.</param>
/// <param name="UserId">The linked account when <paramref name="Kind"/> is <see cref="LinkResolutionKind.Linked"/>; otherwise <see langword="null"/>.</param>
public sealed record LinkResolution(Guid Id, Guid GroupId, Guid MemberId, LinkResolutionKind Kind, Guid? UserId);

/// <summary>The outcome of accepting a member-link request.</summary>
public enum AcceptResult
{
    /// <summary>The link was established.</summary>
    Linked = 0,

    /// <summary>The request was not addressed to the caller, or no longer exists / has expired.</summary>
    NotForCaller = 1,

    /// <summary>The caller (or another account) already backs a member of that group.</summary>
    AlreadyLinked = 2,
}

/// <summary>One published pairing to store: the linked giver's account and the recipient view they may read.</summary>
/// <param name="GiverAccountId">The account of the linked giver who may read this row.</param>
/// <param name="RecipientMemberId">The recipient member, used to compose their current wishlist at read time.</param>
/// <param name="RecipientName">The recipient's display name.</param>
/// <param name="RecipientWishlistSnapshot">The recipient's wishlist as the organizer holds it at publish time.</param>
public sealed record AssignmentPublication(
    Guid GiverAccountId,
    Guid RecipientMemberId,
    string RecipientName,
    IReadOnlyList<string> RecipientWishlistSnapshot);

/// <summary>A resolved assignment view for one caller, composed at read time.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="RecipientName">The caller's recipient.</param>
/// <param name="RecipientWishlist">The recipient's current wishlist (their own row if linked, else the published snapshot).</param>
/// <param name="Budget">The gift budget, or <see langword="null"/>.</param>
/// <param name="Event">The event details, or <see langword="null"/>.</param>
public sealed record AssignmentView(
    Guid GroupId,
    string GroupName,
    string RecipientName,
    IReadOnlyList<string> RecipientWishlist,
    LinkingBudget? Budget,
    LinkingEvent? Event);

/// <summary>A gift budget as the linking store keeps it (mirrors the Secret-Santa contract without referencing it).</summary>
/// <param name="Amount">The amount.</param>
/// <param name="Currency">The ISO currency code.</param>
public sealed record LinkingBudget(decimal Amount, string Currency);

/// <summary>Event details as the linking store keeps them.</summary>
/// <param name="Date">The optional date.</param>
/// <param name="Location">The location.</param>
/// <param name="Description">The description.</param>
public sealed record LinkingEvent(DateTimeOffset? Date, string Location, string Description);

/// <summary>How an authorized single-assignment read resolved.</summary>
public enum AssignmentReadStatus
{
    /// <summary>The caller's own assignment was found and is returned.</summary>
    Found = 0,

    /// <summary>The caller is a linked member of the group but no draw has been published — nothing to show, denied nothing.</summary>
    NotDrawn = 1,

    /// <summary>
    /// The caller is <strong>not</strong> a linked member of that group. This is denied, not filtered
    /// to empty: a stranger probing a group they are not in must be refused, never handed silence that
    /// reads the same as "no draw yet" (NFR-002, ADR-0020).
    /// </summary>
    Denied = 2,
}

/// <summary>The result of an authorized my-assignment read.</summary>
/// <param name="Status">Whether the read was found, undrawn, or denied.</param>
/// <param name="Assignment">The assignment when <paramref name="Status"/> is <see cref="AssignmentReadStatus.Found"/>; otherwise <see langword="null"/>.</param>
public sealed record AssignmentReadResult(AssignmentReadStatus Status, AssignmentView? Assignment);

/// <summary>One group the caller is a linked member of.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group name.</param>
public sealed record LinkedMembership(Guid GroupId, string GroupName);

/// <summary>One of the caller's self-owned wishlists.</summary>
/// <param name="GroupId">The group the wishlist belongs to.</param>
/// <param name="GroupName">The group name.</param>
/// <param name="Items">The wishlist items, in order.</param>
public sealed record SelfWishlist(Guid GroupId, string GroupName, IReadOnlyList<string> Items);

/// <summary>
/// The one server-side surface ADR-0020 sanctioned: a per-caller-authorized store for member-links,
/// the minimal assignment projection, and linked members' self-owned wishlists. It sits beside — never
/// inside — the opaque M3 sync store, and it is the only place the Main host understands the
/// Secret-Santa schema (FEAT-005).
/// </summary>
public interface ILinkingStore
{
    /// <summary>Records a pending member-link request. The mail is sent by the host, out of band (NFR-005).</summary>
    Task CreateMemberLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken);

    /// <summary>Records a member-link that is immediately <see cref="MemberLinkState.Linked"/> (a self-link) and emits its resolution.</summary>
    Task CreateSelfLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Records a member-link that is immediately <see cref="MemberLinkState.Linked"/> because the two
    /// accounts share a circle (create-from-circle, FR-027) — the consent basis ADR-0018 is built on.
    /// Unlike a self-link it emits <strong>no</strong> resolution: the organizer's client already set
    /// <c>Member.UserId</c> when it composed the group, so the link only needs to exist server-side for
    /// wishlist ownership and the authorized reads. The one-user-per-group guard still applies.
    /// </summary>
    Task CreateCircleLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken);

    /// <summary>Lists the pending member-links addressed to <paramref name="targetAccountId"/>.</summary>
    Task<IReadOnlyList<PendingMemberLink>> GetPendingForTargetAsync(Guid targetAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts a member-link on behalf of the caller, enforcing that the request was addressed to them
    /// and the one-user-per-group guard, and emits the link resolution for the organizer's client.
    /// </summary>
    Task<AcceptResult> AcceptAsync(Guid requestId, Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>Declines a member-link on behalf of the caller. Indistinguishable from expiry to the organizer.</summary>
    Task DeclineAsync(Guid requestId, Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a pending request or unlinks an established link <em>by request id</em>, on behalf of the
    /// organizer. Unlinking clears the account's assignment and wishlist rows for that group and emits an
    /// unlink resolution; the member and its wishlist content survive on the organizer's group document.
    /// </summary>
    Task RevokeOrUnlinkAsync(Guid requestId, Guid organizerAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Unlinks an established link addressed by <em>group and member</em> — the pair the organizer's
    /// client naturally holds — on behalf of the organizer. Same effect as the id-addressed unlink.
    /// </summary>
    Task UnlinkMemberAsync(Guid groupId, Guid memberId, Guid organizerAccountId, CancellationToken cancellationToken);

    /// <summary>Drains the pending link resolutions for groups owned by <paramref name="organizerAccountId"/>.</summary>
    Task<IReadOnlyList<LinkResolution>> DrainResolutionsAsync(Guid organizerAccountId, CancellationToken cancellationToken);

    /// <summary>Marks resolutions delivered once the organizer's client has applied them to its group document.</summary>
    Task AckResolutionsAsync(IReadOnlyList<Guid> resolutionIds, Guid organizerAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes the assignment projection for a group's linked members, replacing any prior draw's rows
    /// so a linked member never reads a stale pairing (FR-029). Authorized to the organizer of the group.
    /// </summary>
    Task PublishAssignmentsAsync(
        Guid groupId,
        string groupName,
        Guid organizerAccountId,
        long drawVersion,
        LinkingBudget? budget,
        LinkingEvent? eventDetails,
        IReadOnlyList<AssignmentPublication> assignments,
        CancellationToken cancellationToken);

    /// <summary>Reads the caller's own assignment in one group, denying — not filtering — a non-member (NFR-002).</summary>
    Task<AssignmentReadResult> GetMyAssignmentAsync(Guid groupId, Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>Reads all the caller's own assignments across the groups they are linked in (one per group).</summary>
    Task<IReadOnlyList<AssignmentView>> GetMyAssignmentsAsync(Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>Stores the caller's own wishlist for one membership. The caller must be a linked member of the group.</summary>
    /// <returns><see langword="true"/> when stored; <see langword="false"/> when the caller is not a linked member (denied).</returns>
    Task<bool> UpsertMyWishlistAsync(Guid groupId, Guid callerAccountId, IReadOnlyList<string> items, CancellationToken cancellationToken);

    /// <summary>Reads the caller's self-owned wishlists across their memberships.</summary>
    Task<IReadOnlyList<SelfWishlist>> GetMyWishlistsAsync(Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>Lists the groups the caller is a linked member of, so they can own a wishlist there even before the draw.</summary>
    Task<IReadOnlyList<LinkedMembership>> GetMyMembershipsAsync(Guid callerAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes everything the linking store holds for a departing account and unlinks every member that
    /// referenced it: clears the account's assignment and wishlist rows, closes its member-links, and
    /// emits an unlink resolution to each organizer so the group document degrades gracefully (FR-015).
    /// </summary>
    Task CleanUpAccountAsync(Guid userId, CancellationToken cancellationToken);
}
