using CircleTools.SecretSanta.LocalStore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.Ui;

/// <summary>One event of a group this device joined, for the personal screens (Q5, ADR-0080).</summary>
/// <param name="GroupId">The joined group.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="EventId">The event.</param>
/// <param name="EventName">The event's name (may be empty).</param>
/// <param name="Drawn">Whether the event has a current draw holding the participant's own pairing.</param>
/// <param name="WishlistFilled">Whether the participant's own wishlist for the event has items.</param>
public sealed record JoinedEvent(Guid GroupId, string GroupName, Guid EventId, string EventName, bool Drawn, bool WishlistFilled);

/// <summary>
/// The events of the groups this device joined through a join link (ADR-0080), read from the local copies —
/// so <em>My wishlists</em> and <em>My assignments</em> include them next to the account-linked memberships.
/// </summary>
public static class JoinedGroups
{
    /// <summary>Reads the joined groups' events visible to the current account.</summary>
    /// <param name="services">The page's service provider.</param>
    /// <param name="accountId">The signed-in account, or <see cref="Guid.Empty"/> when signed out.</param>
    /// <returns>Every event of every joined group whose member this device knows.</returns>
    public static async Task<IReadOnlyList<JoinedEvent>> ForAsync(IServiceProvider services, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.GetService<ILocalSyncStore>() is not { } local)
        {
            return [];
        }

        var events = new List<JoinedEvent>();
        foreach (var record in await local.LoadGroupsAsync())
        {
            // A group joined with another account than the signed-in one is not this person's.
            if (!record.IsJoinOnly() || record.ClaimedMemberId is not { } me
                || (record.JoinedAccount is { } joinedBy && joinedBy != accountId))
            {
                continue;
            }

            foreach (var occasion in record.State.Events ?? [])
            {
                var drawn = occasion.Draws.Count > 0 && occasion.Draws[^1].Assignments.Any(a => a.GiverMemberId == me);
                var filled = occasion.Wishlists.Any(w => w.MemberId == me && w.Items.Count > 0);
                events.Add(new JoinedEvent(record.State.Id, record.State.Name, occasion.Id, occasion.Name, drawn, filled));
            }
        }

        return events;
    }
}
