namespace AppSuite.SecretSanta.Core.Persistence;

/// <summary>Serializable snapshot of a <c>Budget</c> value object.</summary>
/// <param name="Amount">The budget amount.</param>
/// <param name="Currency">The currency code.</param>
public sealed record BudgetState(decimal Amount, string Currency);

/// <summary>Serializable snapshot of an <c>EventDetails</c> value object.</summary>
/// <param name="Date">The optional event date.</param>
/// <param name="Location">The event location.</param>
/// <param name="Description">The event description.</param>
public sealed record EventDetailsState(DateTimeOffset? Date, string Location, string Description);

/// <summary>Serializable snapshot of a <c>Member</c>.</summary>
/// <param name="Id">The member identity.</param>
/// <param name="Name">The member name.</param>
/// <param name="Wishlist">The wishlist items.</param>
public sealed record MemberState(Guid Id, string Name, IReadOnlyList<string> Wishlist);

/// <summary>Serializable snapshot of an <c>Exclusion</c>.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
public sealed record ExclusionState(Guid GiverMemberId, Guid RecipientMemberId);

/// <summary>Serializable snapshot of an <c>Assignment</c>.</summary>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The recipient member identity.</param>
public sealed record AssignmentState(Guid GiverMemberId, Guid RecipientMemberId);

/// <summary>Serializable snapshot of a <c>Draw</c>.</summary>
/// <param name="Id">The draw identity.</param>
/// <param name="CreatedAt">The instant the draw was produced.</param>
/// <param name="Assignments">The draw's assignments.</param>
public sealed record DrawState(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<AssignmentState> Assignments);

/// <summary>
/// Serializable memento of the whole <c>Group</c> aggregate. Lets persistence adapters store and
/// rehydrate a group without depending on the domain's private structure.
/// </summary>
/// <param name="Id">The group identity.</param>
/// <param name="Name">The group name.</param>
/// <param name="Budget">The optional budget.</param>
/// <param name="Event">The optional event details.</param>
/// <param name="Members">The members.</param>
/// <param name="Exclusions">The exclusions.</param>
/// <param name="Draws">The ordered draw history.</param>
/// <param name="DrawInvalidatedByMembershipChange">
/// Whether a draw was discarded because the member set changed. Snapshots written before this
/// field existed simply deserialize to <see langword="false"/>.
/// </param>
public sealed record GroupState(
    Guid Id,
    string Name,
    BudgetState? Budget,
    EventDetailsState? Event,
    IReadOnlyList<MemberState> Members,
    IReadOnlyList<ExclusionState> Exclusions,
    IReadOnlyList<DrawState> Draws,
    bool DrawInvalidatedByMembershipChange = false);
