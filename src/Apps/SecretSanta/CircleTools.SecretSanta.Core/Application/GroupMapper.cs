using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Maps <see cref="Group"/> aggregates to their contract DTOs. Lives in the application layer.</summary>
internal static class GroupMapper
{
    public static GroupSummaryDto ToSummary(Group group) =>
        new(
            group.Id,
            group.Name,
            group.Members.Count,
            group.CurrentDraw is not null,
            group.DrawInvalidatedByMembershipChange);

    public static GroupDto ToDto(Group group)
    {
        var members = group.Members
            .Select(m => new MemberDto(m.Id, m.Name, m.Wishlist.Items, m.UserId))
            .ToList();

        var exclusions = group.Exclusions
            .Select(e => new ExclusionDto(e.GiverMemberId, e.RecipientMemberId))
            .ToList();

        var assignments = group.CurrentDraw is null
            ? []
            : group.CurrentDraw.Assignments
                .Select(a => new AssignmentDto(
                    a.GiverMemberId,
                    group.FindMember(a.GiverMemberId)?.Name ?? string.Empty,
                    a.RecipientMemberId,
                    group.FindMember(a.RecipientMemberId)?.Name ?? string.Empty))
                .ToList();

        return new GroupDto(
            group.Id,
            group.Name,
            group.Budget is null ? null : new BudgetDto(group.Budget.Amount, group.Budget.Currency),
            group.EventDetails is null
                ? null
                : new EventDetailsDto(group.EventDetails.Date, group.EventDetails.Location, group.EventDetails.Description),
            members,
            exclusions,
            group.CurrentDraw is not null,
            assignments,
            group.DrawInvalidatedByMembershipChange);
    }
}
