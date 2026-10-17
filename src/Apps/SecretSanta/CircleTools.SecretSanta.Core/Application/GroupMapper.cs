using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Maps <see cref="Group"/> aggregates and their <see cref="Event"/>s to contract DTOs. Application layer.</summary>
internal static class GroupMapper
{
    public static GroupSummaryDto ToSummary(Group group, DateTimeOffset now) =>
        new(
            group.Id,
            group.Name,
            group.Members.Count,
            group.Events.Count,
            group.Events.Any(e => e.CurrentDraw is not null),
            group.Events.Any(e => IsStaleAndActionable(e, now)));

    public static GroupDto ToDto(Group group, DateTimeOffset now)
    {
        var members = group.Members
            .Select(m => new MemberDto(m.Id, m.Name, m.UserId, m.GiftNote))
            .ToList();

        var exclusions = group.Exclusions
            .Select(e => new ExclusionDto(e.GiverMemberId, e.RecipientMemberId, e.Mutual))
            .ToList();

        var events = group.Events.Select(e => ToEventSummary(group, e, now)).ToList();

        return new GroupDto(group.Id, group.Name, members, exclusions, events);
    }

    public static EventSummaryDto ToEventSummary(Group group, Event occasion, DateTimeOffset now) =>
        new(
            occasion.Id,
            occasion.Name,
            occasion.CurrentDraw is not null,
            IsStaleAndActionable(occasion, now),
            occasion.EventDetails?.Date,
            group.Members.Count(m => occasion.WishlistFor(m.Id).Items.Count > 0),
            BreaksExclusions(group, occasion));

    // A current draw that pairs someone an exclusion (added after the draw) forbids (Q13b).
    private static bool BreaksExclusions(Group group, Event occasion) =>
        occasion.CurrentDraw is { } draw
        && draw.Assignments.Any(a => group.Exclusions.Any(e => e.Forbids(a.GiverMemberId, a.RecipientMemberId)));

    // A draw is "stale" the moment the roster changes (ADR-0052), but that is only worth surfacing while
    // the occasion is still upcoming: a past exchange (its date has come and gone) is history, not
    // something to re-draw, so the "re-draw needed" flag is suppressed once the date is in the past. An
    // undated occasion is treated as ongoing, so it still shows the flag.
    private static bool IsStaleAndActionable(Event occasion, DateTimeOffset now) =>
        occasion.DrawInvalidatedByMembershipChange
        && (occasion.EventDetails?.Date is not { } date || date.Date >= now.Date);

    public static EventDto ToEventDto(Group group, Event occasion, DateTimeOffset now)
    {
        var assignments = occasion.CurrentDraw is null
            ? []
            : occasion.CurrentDraw.Assignments
                .Select(a => new AssignmentDto(
                    a.GiverMemberId,
                    group.FindMember(a.GiverMemberId)?.Name ?? string.Empty,
                    a.RecipientMemberId,
                    group.FindMember(a.RecipientMemberId)?.Name ?? string.Empty))
                .ToList();

        // One wishlist row per current member, so the per-event wishlists screen shows the whole roster.
        var wishlists = group.Members
            .Select(m => new MemberWishlistDto(m.Id, m.Name, occasion.WishlistFor(m.Id).Items))
            .ToList();

        return new EventDto(
            occasion.Id,
            group.Id,
            occasion.Name,
            occasion.Budget is null ? null : new BudgetDto(occasion.Budget.Amount, occasion.Budget.Currency, occasion.Budget.MinAmount),
            occasion.EventDetails is null
                ? null
                : new EventDetailsDto(occasion.EventDetails.Date, occasion.EventDetails.Location, occasion.EventDetails.Description),
            occasion.CurrentDraw is not null,
            assignments,
            IsStaleAndActionable(occasion, now),
            wishlists,
            occasion.BaselineDraw is not null,
            BreaksExclusions(group, occasion));
    }
}
