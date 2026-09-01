namespace CircleTools.SecretSanta.Contracts;

/// <summary>Summary view of a group for list screens (ADR-0042: a group owns a shared roster and events).</summary>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="MemberCount">The number of members in the shared roster.</param>
/// <param name="EventCount">The number of events (occasions) in the group.</param>
/// <param name="HasAnyDraw">Whether at least one event has a current (most recent) draw.</param>
/// <param name="AnyDrawInvalidated">Whether at least one event's draw was voided by a member being added or removed.</param>
public sealed record GroupSummaryDto(
    Guid Id,
    string Name,
    int MemberCount,
    int EventCount,
    bool HasAnyDraw,
    bool AnyDrawInvalidated);

/// <summary>Gift budget for an event.</summary>
/// <param name="Amount">The monetary amount.</param>
/// <param name="Currency">The ISO currency code (e.g. CHF).</param>
public sealed record BudgetDto(decimal Amount, string Currency);

/// <summary>Details describing a gift-exchange occasion.</summary>
/// <param name="Date">The optional event date and time.</param>
/// <param name="Location">The event location (may be empty).</param>
/// <param name="Description">Free-text description (may be empty).</param>
public sealed record EventDetailsDto(DateTimeOffset? Date, string Location, string Description);

/// <summary>A group member (shared-roster identity). Wishlists are per event (ADR-0042).</summary>
/// <param name="Id">The member identity.</param>
/// <param name="Name">The member name.</param>
/// <param name="UserId">
/// The account this member is linked to, or <see langword="null"/> when unlinked. Lets list and
/// editor screens show link state without a second lookup.
/// </param>
public sealed record MemberDto(Guid Id, string Name, Guid? UserId = null);

/// <summary>
/// An exclusion between two members. When <paramref name="Mutual"/> is <see langword="false"/> it is
/// one-way (<paramref name="GiverMemberId"/> must not draw <paramref name="RecipientMemberId"/>); when
/// <see langword="true"/> neither member may be assigned the other.
/// </summary>
/// <param name="GiverMemberId">The giver member identity (an endpoint when mutual).</param>
/// <param name="RecipientMemberId">The forbidden recipient member identity (the other endpoint when mutual).</param>
/// <param name="Mutual">Whether the exclusion applies in both directions.</param>
public sealed record ExclusionDto(Guid GiverMemberId, Guid RecipientMemberId, bool Mutual = false);

/// <summary>A single giver-to-recipient assignment within a draw.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="GiverName">The giver name.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
/// <param name="RecipientName">The recipient name.</param>
public sealed record AssignmentDto(Guid GiverMemberId, string GiverName, Guid RecipientMemberId, string RecipientName);

/// <summary>Summary of one event (occasion) within a group, for the events list and sub-navigation.</summary>
/// <param name="Id">The event identity.</param>
/// <param name="Name">The event name (may be empty; the UI shows a generic label for an unnamed event).</param>
/// <param name="HasCurrentDraw">Whether this event has a current draw.</param>
/// <param name="DrawInvalidatedByMembershipChange">Whether this event's draw needs re-running because the roster changed while it is still upcoming.</param>
/// <param name="Date">The event's date, if set — used to order occasions for quick access (nav).</param>
public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    bool HasCurrentDraw,
    bool DrawInvalidatedByMembershipChange,
    DateTimeOffset? Date = null);

/// <summary>Group-level view for the editor screens: the group name, its shared roster and exclusions, and its events.</summary>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="Members">The shared group members.</param>
/// <param name="Exclusions">The shared configured exclusions.</param>
/// <param name="Events">The group's events, oldest first.</param>
public sealed record GroupDto(
    Guid Id,
    string Name,
    IReadOnlyList<MemberDto> Members,
    IReadOnlyList<ExclusionDto> Exclusions,
    IReadOnlyList<EventSummaryDto> Events);

/// <summary>One member's wishlist within an event.</summary>
/// <param name="MemberId">The member the wishlist belongs to.</param>
/// <param name="MemberName">The member's display name.</param>
/// <param name="Items">The wishlist items, in order (may be empty).</param>
public sealed record MemberWishlistDto(Guid MemberId, string MemberName, IReadOnlyList<string> Items);

/// <summary>Full view of one event (occasion) for the event editor, draw and share screens.</summary>
/// <param name="Id">The event identity.</param>
/// <param name="GroupId">The owning group's identity.</param>
/// <param name="Name">The event name (may be empty).</param>
/// <param name="Budget">The event's optional gift budget.</param>
/// <param name="Details">The event's optional details (date, location, description).</param>
/// <param name="HasCurrentDraw">Whether this event has a current draw.</param>
/// <param name="CurrentAssignments">The assignments of this event's current draw (empty when none).</param>
/// <param name="DrawInvalidatedByMembershipChange">Whether this event's draw was voided by a roster change.</param>
/// <param name="Wishlists">Each member's wishlist for this event (one row per member; items may be empty).</param>
public sealed record EventDto(
    Guid Id,
    Guid GroupId,
    string Name,
    BudgetDto? Budget,
    EventDetailsDto? Details,
    bool HasCurrentDraw,
    IReadOnlyList<AssignmentDto> CurrentAssignments,
    bool DrawInvalidatedByMembershipChange,
    IReadOnlyList<MemberWishlistDto> Wishlists);

/// <summary>Outcome of a draw attempt.</summary>
/// <param name="Success">Whether a valid draw was produced.</param>
/// <param name="FailureReason">The failure reason code when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="AssignmentCount">The number of assignments produced on success.</param>
public sealed record RunDrawResultDto(bool Success, string? FailureReason, int AssignmentCount);

/// <summary>A reveal link for one member, with a human-readable owner label.</summary>
/// <param name="ForLabel">The readable "for" label identifying the giver the link belongs to.</param>
/// <param name="Url">The full reveal URL, with the payload carried in the fragment.</param>
public sealed record RevealLinkDto(string ForLabel, string Url);
