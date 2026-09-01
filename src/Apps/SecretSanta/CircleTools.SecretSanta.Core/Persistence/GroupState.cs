namespace CircleTools.SecretSanta.Core.Persistence;

/// <summary>Serializable snapshot of a <c>Budget</c> value object.</summary>
/// <param name="Amount">The budget amount.</param>
/// <param name="Currency">The currency code.</param>
public sealed record BudgetState(decimal Amount, string Currency);

/// <summary>Serializable snapshot of an <c>EventDetails</c> value object.</summary>
/// <param name="Date">The optional event date.</param>
/// <param name="Location">The event location.</param>
/// <param name="Description">The event description.</param>
public sealed record EventDetailsState(DateTimeOffset? Date, string Location, string Description);

/// <summary>Serializable snapshot of a <c>Member</c> (shared roster identity).</summary>
/// <param name="Id">The member identity.</param>
/// <param name="Name">The member name.</param>
/// <param name="Wishlist">
/// <strong>Legacy.</strong> Before ADR-0042 a wishlist lived on the member; it is now per event. This
/// field is only read when migrating a pre-events snapshot (its items become the member's wishlist in
/// the synthesized default event) and is never written again — new snapshots leave it <see langword="null"/>.
/// </param>
/// <param name="UserId">
/// The account this member is linked to, or <see langword="null"/> when unlinked. Snapshots written
/// before this field existed deserialize to <see langword="null"/> (unlinked), so no group is lost
/// by the schema bump.
/// </param>
/// <param name="OriginCirclePersonId">
/// The circle person this member was copied from, or <see langword="null"/>. Recorded at create time
/// and never dereferenced; older snapshots deserialize to <see langword="null"/>.
/// </param>
/// <param name="IsArchived">
/// Whether this member has been archived — removed from the active roster but kept for history
/// (ADR-0052). Snapshots written before this field existed deserialize to <see langword="false"/>
/// (active), so no existing member is lost or hidden by the schema bump.
/// </param>
public sealed record MemberState(
    Guid Id,
    string Name,
    IReadOnlyList<string>? Wishlist = null,
    Guid? UserId = null,
    Guid? OriginCirclePersonId = null,
    bool IsArchived = false);

/// <summary>Serializable snapshot of an <c>Exclusion</c>.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
/// <param name="Mutual">
/// Whether the exclusion applies in both directions. Snapshots written before this field existed
/// deserialize to <see langword="false"/> (one-way), so no existing exclusion changes meaning.
/// </param>
public sealed record ExclusionState(Guid GiverMemberId, Guid RecipientMemberId, bool Mutual = false);

/// <summary>Serializable snapshot of an <c>Assignment</c>.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
public sealed record AssignmentState(Guid GiverMemberId, Guid RecipientMemberId);

/// <summary>Serializable snapshot of a <c>Draw</c>.</summary>
/// <param name="Id">The draw identity.</param>
/// <param name="CreatedAt">The instant the draw was produced.</param>
/// <param name="Assignments">The draw's assignments.</param>
public sealed record DrawState(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<AssignmentState> Assignments);

/// <summary>Serializable snapshot of one member's wishlist within an event (ADR-0042).</summary>
/// <param name="MemberId">The member the wishlist belongs to.</param>
/// <param name="Items">The wishlist items, in order.</param>
public sealed record EventWishlistState(Guid MemberId, IReadOnlyList<string> Items);

/// <summary>
/// Serializable snapshot of an <c>Event</c> — one occasion within a group (ADR-0042). Carries the
/// occasion's own budget, event details, draw history, membership-invalidation flag, and its per-member
/// wishlists.
/// </summary>
/// <param name="Id">The event identity (the group's first/default event shares the group id).</param>
/// <param name="Name">The event name (may be empty).</param>
/// <param name="Budget">The optional budget for this event.</param>
/// <param name="EventDetails">The optional event details for this event.</param>
/// <param name="Draws">The event's ordered draw history.</param>
/// <param name="DrawInvalidatedByMembershipChange">Whether this event's draw was voided by a roster change.</param>
/// <param name="Wishlists">The per-member wishlists for this event.</param>
public sealed record EventState(
    Guid Id,
    string Name,
    BudgetState? Budget,
    EventDetailsState? EventDetails,
    IReadOnlyList<DrawState> Draws,
    bool DrawInvalidatedByMembershipChange,
    IReadOnlyList<EventWishlistState> Wishlists);

/// <summary>
/// Serializable memento of the whole <c>Group</c> aggregate. Lets persistence adapters store and
/// rehydrate a group without depending on the domain's private structure.
/// </summary>
/// <remarks>
/// The <paramref name="Budget"/>, <paramref name="Event"/> and <paramref name="Draws"/> fields, and
/// <c>DrawInvalidatedByMembershipChange</c>, are <strong>legacy</strong> single-exchange fields kept
/// only so a pre-ADR-0042 snapshot can be read and migrated into <paramref name="Events"/>. A new
/// snapshot leaves them null/empty and carries everything under <paramref name="Events"/>; a snapshot
/// written before events existed has a null/absent <paramref name="Events"/> and is migrated on load.
/// </remarks>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="Budget">Legacy: the single group budget (migrated into the default event).</param>
/// <param name="Event">Legacy: the single group event details (migrated into the default event).</param>
/// <param name="Members">The shared members.</param>
/// <param name="Exclusions">The shared exclusions.</param>
/// <param name="Draws">Legacy: the single group draw history (migrated into the default event).</param>
/// <param name="DrawInvalidatedByMembershipChange">Legacy: the single group's draw-invalidation flag.</param>
/// <param name="Events">The group's events; null/absent in a pre-events snapshot, migrated on load.</param>
public sealed record GroupState(
    Guid Id,
    string Name,
    BudgetState? Budget,
    EventDetailsState? Event,
    IReadOnlyList<MemberState> Members,
    IReadOnlyList<ExclusionState> Exclusions,
    IReadOnlyList<DrawState> Draws,
    bool DrawInvalidatedByMembershipChange = false,
    IReadOnlyList<EventState>? Events = null);
