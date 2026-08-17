using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Maps <see cref="Group"/> aggregates and their <see cref="Event"/>s to contract DTOs. Application layer.</summary>
internal static class GroupMapper
{
    public static GroupSummaryDto ToSummary(Group group) =>
        new(
            group.Id,
            group.Name,
            group.Members.Count,
            group.Events.Count,
            group.Events.Any(e => e.CurrentDraw is not null),
            group.Events.Any(e => e.DrawInvalidatedByMembershipChange));

    public static GroupDto ToDto(Group group)
    {
        var members = group.Members
            .Select(m => new MemberDto(m.Id, m.Name, m.UserId))
            .ToList();

        var exclusions = group.Exclusions
            .Select(e => new ExclusionDto(e.GiverMemberId, e.RecipientMemberId, e.Mutual))
            .ToList();

        var events = group.Events.Select(ToEventSummary).ToList();

        return new GroupDto(group.Id, group.Name, members, exclusions, events);
    }

    public static EventSummaryDto ToEventSummary(Event occasion) =>
        new(occasion.Id, occasion.Name, occasion.CurrentDraw is not null, occasion.DrawInvalidatedByMembershipChange);

    public static EventDto ToEventDto(Group group, Event occasion)
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
            occasion.Budget is null ? null : new BudgetDto(occasion.Budget.Amount, occasion.Budget.Currency),
            occasion.EventDetails is null
                ? null
                : new EventDetailsDto(occasion.EventDetails.Date, occasion.EventDetails.Location, occasion.EventDetails.Description),
            occasion.CurrentDraw is not null,
            assignments,
            occasion.DrawInvalidatedByMembershipChange,
            wishlists);
    }
}
