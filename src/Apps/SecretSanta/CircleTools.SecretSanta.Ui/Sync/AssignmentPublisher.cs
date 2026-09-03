using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore.Linking;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Publishes the assignment projection for a group's <em>linked</em> givers after a draw or re-run
/// (FEAT-005, FR-029). The organizer's client holds the full mapping; it sends the server only the
/// minimal per-user row ADR-0020 permits — giver account, recipient view, group — and the server
/// replaces any prior draw's rows so a linked member's next read is the current pairing, never a
/// previous one.
/// </summary>
/// <remarks>
/// The draw version is the draw's creation instant in Unix milliseconds: monotonic across re-runs
/// (a later draw always has a later instant), which is exactly what the server's replace-only-if-newer
/// rule needs. Nothing is published for an undrawn group, and a group with no linked givers publishes
/// nothing — the account-free and fully-unlinked cases stay entirely inert (NFR-001).
/// </remarks>
/// <param name="api">The linking surface client.</param>
public sealed class AssignmentPublisher(ILinkingApi api)
{
    /// <summary>
    /// Publishes a group's linking projection: its event registry (so linked members can enumerate
    /// events for their wishlists) and, for each drawn event, the linked-giver assignment rows (ADR-0042).
    /// A group with no linked members publishes nothing — the account-free and fully-unlinked cases stay
    /// inert (NFR-001).
    /// </summary>
    /// <param name="group">The group to publish.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The number of assignment rows published across the group's events.</returns>
    public async Task<int> PublishAsync(Group group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Nothing to project for a group nobody has linked into.
        if (group.Members.All(m => m.UserId is null))
        {
            return 0;
        }

        // The event registry lets a linked member on another device pick an event to wish for, even
        // before a draw. Published every cycle so a rename or a removed event lands.
        await api.PublishGroupEventsAsync(
            group.Id,
            new PublishGroupEventsRequest(
                group.Name,
                group.Events.Select(e => new GroupEventEntry(e.Id, e.Name, e.EventDetails?.Date)).ToList()),
            cancellationToken);

        var published = 0;
        foreach (var occasion in group.Events.Where(e => e.CurrentDraw is not null))
        {
            published += await PublishEventAsync(group, occasion, cancellationToken);
        }

        return published;
    }

    private async Task<int> PublishEventAsync(Group group, Event occasion, CancellationToken cancellationToken)
    {
        var draw = occasion.CurrentDraw!;
        var rows = new List<PublishedAssignment>();
        foreach (var assignment in draw.Assignments)
        {
            var giver = group.FindMember(assignment.GiverMemberId);
            if (giver?.UserId is not { } giverUserId)
            {
                continue; // Only linked givers get a server-side row.
            }

            var recipient = group.FindMember(assignment.RecipientMemberId);
            if (recipient is null)
            {
                continue;
            }

            rows.Add(new PublishedAssignment(giverUserId, recipient.Id, recipient.Name, [.. occasion.WishlistFor(recipient.Id).Items]));
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var request = new PublishAssignmentsRequest(
            group.Id,
            group.Name,
            occasion.Id,
            occasion.Name,
            draw.CreatedAt.ToUnixTimeMilliseconds(),
            occasion.Budget is null ? null : new BudgetDto(occasion.Budget.Amount, occasion.Budget.Currency, occasion.Budget.MinAmount),
            occasion.EventDetails is null
                ? null
                : new EventDetailsDto(occasion.EventDetails.Date, occasion.EventDetails.Location, occasion.EventDetails.Description),
            rows);

        await api.PublishAssignmentsAsync(group.Id, request, cancellationToken);
        return rows.Count;
    }
}
