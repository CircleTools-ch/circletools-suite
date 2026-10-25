using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.Service.Core.Entries;

/// <summary>
/// Writes members' own entries into a group document (ADR-0086), through the domain model rather than by JSON
/// property names. Each entry replaces that member's gift note and their wishlist for every event it names;
/// members or events the document doesn't hold are skipped.
/// </summary>
public static class MemberEntryMerge
{
    /// <summary>Applies the entries to a document.</summary>
    /// <param name="document">The group document.</param>
    /// <param name="entries">The members' entries.</param>
    /// <returns>The merged document, and whether anything changed.</returns>
    public static (GroupState Document, bool Changed) Apply(GroupState document, IEnumerable<MemberEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        if (list.Count == 0)
        {
            return (document, false);
        }

        var group = Group.FromState(document);
        foreach (var entry in list)
        {
            if (group.FindMember(entry.MemberId) is null)
            {
                continue;
            }

            group.SetMemberGiftNote(entry.MemberId, entry.GiftNote);
            foreach (var wishlist in entry.Wishlists ?? [])
            {
                if (group.FindEvent(wishlist.EventId) is not null)
                {
                    group.SetMemberWishlist(wishlist.EventId, entry.MemberId, Wishlist.Create(wishlist.Items));
                }
            }
        }

        var merged = group.ToState();
        var changed = GroupDocument.Serialize(merged) != GroupDocument.Serialize(Group.FromState(document).ToState());
        return (changed ? merged : document, changed);
    }
}
