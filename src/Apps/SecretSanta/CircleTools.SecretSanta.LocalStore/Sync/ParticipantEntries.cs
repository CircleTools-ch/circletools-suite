using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// A joined participant's own entry — gift note and per-event wishlists (ADR-0080) — read from and applied to
/// a group memento, the same way the server merges it, so the participant's device shows its own edits at once.
/// </summary>
public static class ParticipantEntries
{
    /// <summary>Reads the member's current entry from a group memento.</summary>
    /// <param name="state">The group.</param>
    /// <param name="memberId">The participant's member.</param>
    /// <returns>The member's gift note and their wishlist in every event that has one.</returns>
    public static ParticipantEntry Read(GroupState state, Guid memberId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var note = state.Members.FirstOrDefault(m => m.Id == memberId)?.GiftNote;
        var wishlists = (state.Events ?? [])
            .Select(e => (e.Id, Items: e.Wishlists.FirstOrDefault(w => w.MemberId == memberId)?.Items))
            .Where(x => x.Items is { Count: > 0 })
            .Select(x => new ParticipantWishlist(x.Id, x.Items!))
            .ToList();
        return new ParticipantEntry(note, wishlists);
    }

    /// <summary>
    /// Applies an entry to a group memento: the member's gift note, and their wishlist in each event the entry
    /// names (an empty list clears it). Members or events the group doesn't hold are skipped.
    /// </summary>
    /// <param name="state">The group.</param>
    /// <param name="memberId">The participant's member.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The updated group.</returns>
    public static GroupState Apply(GroupState state, Guid memberId, ParticipantEntry entry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entry);

        var note = string.IsNullOrWhiteSpace(entry.GiftNote) ? null : entry.GiftNote.Trim();
        var members = state.Members
            .Select(m => m.Id == memberId ? m with { GiftNote = note } : m)
            .ToList();

        var byEvent = (entry.Wishlists ?? []).GroupBy(w => w.EventId).ToDictionary(g => g.Key, g => g.Last());
        var events = state.Events?
            .Select(e =>
            {
                if (!byEvent.TryGetValue(e.Id, out var wish))
                {
                    return e;
                }

                var items = (wish.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
                var others = e.Wishlists.Where(w => w.MemberId != memberId);
                return e with
                {
                    Wishlists = items.Count == 0 ? [.. others] : [.. others, new EventWishlistState(memberId, items)],
                };
            })
            .ToList();

        return state with { Members = members, Events = events };
    }
}
