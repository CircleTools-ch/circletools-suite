using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.LocalStore.Linking;

namespace AppSuite.SecretSanta.Ui;

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
    /// <summary>Publishes the current draw's linked-giver rows for a group, if there is anything to publish.</summary>
    /// <param name="group">The group whose current draw is being published.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The number of rows published (0 when the group is undrawn or has no linked givers).</returns>
    public async Task<int> PublishAsync(Group group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.CurrentDraw is not { } draw)
        {
            return 0;
        }

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

            rows.Add(new PublishedAssignment(giverUserId, recipient.Id, recipient.Name, [.. recipient.Wishlist.Items]));
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var request = new PublishAssignmentsRequest(
            group.Id,
            group.Name,
            draw.CreatedAt.ToUnixTimeMilliseconds(),
            group.Budget is null ? null : new BudgetDto(group.Budget.Amount, group.Budget.Currency),
            group.EventDetails is null
                ? null
                : new EventDetailsDto(group.EventDetails.Date, group.EventDetails.Location, group.EventDetails.Description),
            rows);

        await api.PublishAssignmentsAsync(group.Id, request, cancellationToken);
        return rows.Count;
    }
}
