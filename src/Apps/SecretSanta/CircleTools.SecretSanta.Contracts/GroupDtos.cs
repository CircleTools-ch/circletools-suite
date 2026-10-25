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
/// <param name="Amount">The maximum (upper-limit) monetary amount.</param>
/// <param name="Currency">The ISO currency code (e.g. CHF).</param>
/// <param name="MinAmount">The optional minimum (lower-bound) amount, or <see langword="null"/> for a bare maximum (FR-036).</param>
public sealed record BudgetDto(decimal Amount, string Currency, decimal? MinAmount = null);

/// <summary>Details describing a gift-exchange occasion.</summary>
/// <param name="Date">The optional event date and time.</param>
/// <param name="Location">The event location (may be empty).</param>
/// <param name="Description">Free-text description (may be empty).</param>
public sealed record EventDetailsDto(DateTimeOffset? Date, string Location, string Description);

/// <summary>A group member (shared-roster identity). Wishlists are per event (ADR-0042).</summary>
/// <param name="Id">The member identity.</param>
/// <param name="Name">The member name.</param>
/// <param name="GiftNote">
/// The member's stable gift note — allergies, dietary/lifestyle needs, or things not to buy — shown to
/// their giver on the reveal (FR-037). Empty when unset.
/// </param>
public sealed record MemberDto(Guid Id, string Name, string GiftNote = "");

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
/// <param name="WishlistsFilled">How many current members have at least one wishlist item (the card's progress, I5).</param>
/// <param name="DrawBreaksExclusions">
/// Whether the current draw pairs someone an exclusion added since forbids — "no longer matches the
/// exclusions, draw again?" (Q13b).
/// </param>
public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    bool HasCurrentDraw,
    bool DrawInvalidatedByMembershipChange,
    DateTimeOffset? Date = null,
    int WishlistsFilled = 0,
    bool DrawBreaksExclusions = false);

/// <summary>Whether the exclusions still leave a valid draw (Q13a).</summary>
/// <param name="WithoutRecipient">Names of members left with nobody they may give to.</param>
/// <param name="WithoutGiver">Names of members nobody may give to.</param>
/// <param name="Possible">Whether a draw exists; <see langword="null"/> when not judged (fewer than three members, or too large to check quickly).</param>
public sealed record DrawFeasibilityDto(IReadOnlyList<string> WithoutRecipient, IReadOnlyList<string> WithoutGiver, bool? Possible);

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
/// <param name="HasCarriedForwardBaseline">
/// Whether this (still undrawn) event carries a draw baseline from a previous occasion (FR-035), so its
/// first draw avoids last time's pairings. Lets the draw screen offer an "ignore last time" retry if that
/// constraint over-constrains a small roster. <see langword="false"/> once a real draw has run.
/// </param>
/// <param name="DrawBreaksExclusions">Whether the current draw pairs someone a later exclusion forbids (Q13b).</param>
public sealed record EventDto(
    Guid Id,
    Guid GroupId,
    string Name,
    BudgetDto? Budget,
    EventDetailsDto? Details,
    bool HasCurrentDraw,
    IReadOnlyList<AssignmentDto> CurrentAssignments,
    bool DrawInvalidatedByMembershipChange,
    IReadOnlyList<MemberWishlistDto> Wishlists,
    bool HasCarriedForwardBaseline = false,
    bool DrawBreaksExclusions = false);

/// <summary>Outcome of a draw attempt.</summary>
/// <param name="Success">Whether a valid draw was produced.</param>
/// <param name="FailureReason">The failure reason code when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="AssignmentCount">The number of assignments produced on success.</param>
public sealed record RunDrawResultDto(bool Success, string? FailureReason, int AssignmentCount);

/// <summary>A reveal link for one member, with a human-readable owner label.</summary>
/// <param name="ForLabel">The readable "for" label identifying the giver the link belongs to.</param>
/// <param name="GiverMemberId">The giver's member id, so a caller can correlate the link with its reveal token (ADR-0068).</param>
/// <param name="Url">The full reveal URL, with the payload carried in the fragment.</param>
public sealed record RevealLinkDto(string ForLabel, Guid GiverMemberId, string Url);

/// <summary>
/// One giver's reveal slice data (ADR-0068, FEAT-020): the giver's member id plus the reveal payload the
/// backend-backed live reveal publishes. Distinct from <see cref="RevealLinkDto"/>, which is the URL a
/// giver receives; this is the decoded payload a draw-capable client uploads for that giver.
/// </summary>
/// <param name="GiverMemberId">The giver whose slice this is.</param>
/// <param name="Payload">The reveal payload (recipient, wishlist, gift note, budget, event details).</param>
public sealed record RevealSliceData(Guid GiverMemberId, RevealPayload Payload);
