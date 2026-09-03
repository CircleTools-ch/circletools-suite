using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service.Repository.Linking;

/// <summary>
/// A member-link request row (M4, FR-028). Reuses the invitation lifecycle — expiry, revoke,
/// constant-time account lookup — as a variant scoped to a member rather than a circle join. No
/// foreign key to Identity: the account is a value, the same independence rule Circles and sync keep
/// (ADR-0019).
/// </summary>
public sealed class MemberLinkRow
{
    /// <summary>The request identity (client- or server-minted; server-minted here).</summary>
    public Guid Id { get; set; }

    /// <summary>The group the member belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The group name, stored denormalized because the group document is opaque to the server.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>The member being linked.</summary>
    public Guid MemberId { get; set; }

    /// <summary>The member's display name.</summary>
    public string MemberDisplayName { get; set; } = string.Empty;

    /// <summary>The organizer who sent the request and owns the group.</summary>
    public Guid OrganizerAccountId { get; set; }

    /// <summary>The account the invited address resolved to, or <see langword="null"/> when it had none.</summary>
    public Guid? TargetAccountId { get; set; }

    /// <summary>The lifecycle state.</summary>
    public MemberLinkState State { get; set; }

    /// <summary>When the request was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the request stops being acceptable (14 days, mirroring M2 invitations).</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// The minimal per-user assignment projection ADR-0020 permits: giver account, recipient view,
/// group. Keyed so the authorized read is a single row lookup by <see cref="GiverAccountId"/>. A
/// re-run publishes a higher <see cref="DrawVersion"/> and replaces prior rows.
/// </summary>
public sealed class AssignmentRow
{
    /// <summary>The group the pairing belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The event (occasion) the pairing belongs to — part of the authorized read key (ADR-0042).</summary>
    public Guid EventId { get; set; }

    /// <summary>The event name, denormalized (the group document is opaque to the server), shown alongside the assignment.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>The account of the linked giver who may read this row — the authorized read key.</summary>
    public Guid GiverAccountId { get; set; }

    /// <summary>The organizer who published the pairing.</summary>
    public Guid OrganizerAccountId { get; set; }

    /// <summary>The group name, shown to the giver alongside their assignment.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>The recipient member's identity, used to compose their current wishlist at read time.</summary>
    public Guid RecipientMemberId { get; set; }

    /// <summary>The recipient's display name.</summary>
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>The recipient's wishlist as the organizer held it at publish time (the fallback for the current one).</summary>
    public string RecipientWishlistSnapshot { get; set; } = "[]";

    /// <summary>The gift budget maximum amount, or <see langword="null"/>.</summary>
    public decimal? BudgetAmount { get; set; }

    /// <summary>The gift budget currency, or <see langword="null"/>.</summary>
    public string? BudgetCurrency { get; set; }

    /// <summary>The gift budget minimum (lower bound), or <see langword="null"/> for a bare maximum (FR-036).</summary>
    public decimal? BudgetMinAmount { get; set; }

    /// <summary>The recipient's stable gift note shown to the giver (FR-037); empty when unset.</summary>
    public string RecipientGiftNote { get; set; } = string.Empty;

    /// <summary>The event date, or <see langword="null"/>.</summary>
    public DateTimeOffset? EventDate { get; set; }

    /// <summary>The event location, or <see langword="null"/>.</summary>
    public string? EventLocation { get; set; }

    /// <summary>The event description, or <see langword="null"/>.</summary>
    public string? EventDescription { get; set; }

    /// <summary>The draw version this row belongs to; a re-run publishes a higher value and replaces prior rows.</summary>
    public long DrawVersion { get; set; }
}

/// <summary>A linked member's self-owned wishlist for one event of one membership (FR-030, ADR-0042).</summary>
public sealed class MemberWishlistRow
{
    /// <summary>The group the wishlist belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The event (occasion) the wishlist belongs to — part of the key (ADR-0042).</summary>
    public Guid EventId { get; set; }

    /// <summary>The event name, denormalized so the caller's wishlist list needs no group read.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>The linked member's account — the owner of this wishlist.</summary>
    public Guid MemberAccountId { get; set; }

    /// <summary>The group name, so the caller's wishlist list needs no group read.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>The wishlist items as a JSON array (stored as <c>jsonb</c>).</summary>
    public string Items { get; set; } = "[]";

    /// <summary>The per-wishlist version, bumped on each edit for last-write-wins reconciliation (FR-024).</summary>
    public long Version { get; set; }
}

/// <summary>
/// The organizer's event registry for a group (ADR-0042): the list of events (occasions) a linked
/// member may enumerate to write a per-event wishlist even before a draw. Published by the organizer's
/// client each sync cycle, denormalized because the group document is opaque to the server.
/// </summary>
public sealed class GroupEventRow
{
    /// <summary>The group the event belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The event (occasion) identity.</summary>
    public Guid EventId { get; set; }

    /// <summary>The organizer who owns the group and published this event.</summary>
    public Guid OrganizerAccountId { get; set; }

    /// <summary>The group name, denormalized.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>The event name (may be empty).</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>The optional event date, so a linked member can hide past occasions (FR-030).</summary>
    public DateTimeOffset? Date { get; set; }
}

/// <summary>
/// A pending link resolution the organizer's client drains and applies to its group document, then
/// acknowledges. This is how an accepted / self-linked / unlinked / cascaded change reaches the
/// authoritative roster with no second poller (FEAT-005).
/// </summary>
public sealed class LinkResolutionRow
{
    /// <summary>The resolution identity, used to acknowledge it.</summary>
    public Guid Id { get; set; }

    /// <summary>The organizer whose client drains this resolution.</summary>
    public Guid OrganizerAccountId { get; set; }

    /// <summary>The affected group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The affected member.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Whether the member was linked or unlinked.</summary>
    public LinkResolutionKind Kind { get; set; }

    /// <summary>The linked account when <see cref="Kind"/> is <see cref="LinkResolutionKind.Linked"/>; otherwise <see langword="null"/>.</summary>
    public Guid? UserId { get; set; }

    /// <summary>When the resolution was created, so the drain returns them oldest first.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
