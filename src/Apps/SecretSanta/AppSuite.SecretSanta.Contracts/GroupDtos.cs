namespace AppSuite.SecretSanta.Contracts;

/// <summary>Summary view of a group for list screens.</summary>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="MemberCount">The number of members in the group.</param>
/// <param name="HasCurrentDraw">Whether the group has a current (most recent) draw.</param>
/// <param name="DrawInvalidatedByMembershipChange">Whether a draw was voided by a member being added or removed.</param>
public sealed record GroupSummaryDto(
    Guid Id,
    string Name,
    int MemberCount,
    bool HasCurrentDraw,
    bool DrawInvalidatedByMembershipChange);

/// <summary>Gift budget for a group.</summary>
/// <param name="Amount">The monetary amount.</param>
/// <param name="Currency">The ISO currency code (e.g. CHF).</param>
public sealed record BudgetDto(decimal Amount, string Currency);

/// <summary>Details describing the gift-exchange event.</summary>
/// <param name="Date">The optional event date and time.</param>
/// <param name="Location">The event location (may be empty).</param>
/// <param name="Description">Free-text description (may be empty).</param>
public sealed record EventDetailsDto(DateTimeOffset? Date, string Location, string Description);

/// <summary>A group member and their wishlist.</summary>
/// <param name="Id">The member identity.</param>
/// <param name="Name">The member name.</param>
/// <param name="Wishlist">The member's wishlist items.</param>
/// <param name="UserId">
/// The account this member is linked to, or <see langword="null"/> when unlinked. Lets list and
/// editor screens show link state without a second lookup.
/// </param>
public sealed record MemberDto(Guid Id, string Name, IReadOnlyList<string> Wishlist, Guid? UserId = null);

/// <summary>A directed exclusion: <paramref name="GiverMemberId"/> must not draw <paramref name="RecipientMemberId"/>.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The forbidden recipient member identity.</param>
public sealed record ExclusionDto(Guid GiverMemberId, Guid RecipientMemberId);

/// <summary>A single giver-to-recipient assignment within a draw.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="GiverName">The giver name.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
/// <param name="RecipientName">The recipient name.</param>
public sealed record AssignmentDto(Guid GiverMemberId, string GiverName, Guid RecipientMemberId, string RecipientName);

/// <summary>Full view of a group for the editor screens.</summary>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="Budget">The optional gift budget.</param>
/// <param name="Event">The optional event details.</param>
/// <param name="Members">The group members.</param>
/// <param name="Exclusions">The configured exclusions.</param>
/// <param name="HasCurrentDraw">Whether the group has a current (most recent) draw.</param>
/// <param name="CurrentAssignments">The assignments of the current draw (empty when none).</param>
/// <param name="DrawInvalidatedByMembershipChange">Whether a draw was voided by a member being added or removed.</param>
public sealed record GroupDto(
    Guid Id,
    string Name,
    BudgetDto? Budget,
    EventDetailsDto? Event,
    IReadOnlyList<MemberDto> Members,
    IReadOnlyList<ExclusionDto> Exclusions,
    bool HasCurrentDraw,
    IReadOnlyList<AssignmentDto> CurrentAssignments,
    bool DrawInvalidatedByMembershipChange);

/// <summary>Outcome of a draw attempt.</summary>
/// <param name="Success">Whether a valid draw was produced.</param>
/// <param name="FailureReason">The failure reason code when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="AssignmentCount">The number of assignments produced on success.</param>
public sealed record RunDrawResultDto(bool Success, string? FailureReason, int AssignmentCount);

/// <summary>A reveal link for one member, with a human-readable owner label.</summary>
/// <param name="ForLabel">The readable "for" label identifying the giver the link belongs to.</param>
/// <param name="Url">The full reveal URL, with the payload carried in the fragment.</param>
public sealed record RevealLinkDto(string ForLabel, string Url);
