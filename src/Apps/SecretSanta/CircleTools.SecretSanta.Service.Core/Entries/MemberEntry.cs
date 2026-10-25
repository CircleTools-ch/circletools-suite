using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Service.Core.Entries;

/// <summary>Who keeps a member's entry (ADR-0086).</summary>
public enum MemberEntryKeeper
{
    /// <summary>The member's participant (any route in).</summary>
    Participant = 1,

    /// <summary>The holder of the member's reveal link, for a member without a participant (ADR-0084).</summary>
    RevealLink = 2,
}

/// <summary>One event's wishlist in a member entry.</summary>
/// <param name="EventId">The event.</param>
/// <param name="Items">The items, in order; empty clears the wishlist.</param>
public sealed record EventWishlist(Guid EventId, IReadOnlyList<string> Items);

/// <summary>
/// What a member keeps themselves in an online group: their gift note and their wishlist per event (ADR-0086). The
/// server merges it into the group document on every entry write and every organizer push, so it reaches every
/// reader of the document and an older organizer copy can't overwrite it.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="GiftNote">The gift note; null or blank clears it.</param>
/// <param name="Wishlists">The wishlists, one per event the member set one for.</param>
/// <param name="KeptBy">Who keeps it.</param>
public sealed record MemberEntry(Guid MemberId, string? GiftNote, IReadOnlyList<EventWishlist> Wishlists, MemberEntryKeeper KeptBy);

/// <summary>The bounds of a member entry (NFR-009): the same as the organizer's own editing allows.</summary>
public static class MemberEntryLimits
{
    /// <summary>The longest gift note.</summary>
    public const int MaxGiftNoteLength = Member.MaxGiftNoteLength;

    /// <summary>The most events one entry may carry wishlists for.</summary>
    public const int MaxEvents = 50;

    /// <summary>The most items in one wishlist.</summary>
    public const int MaxWishlistItems = 100;

    /// <summary>The longest wishlist item.</summary>
    public const int MaxWishlistItemLength = 500;

    /// <summary>Whether an entry is within bounds.</summary>
    /// <param name="giftNote">The gift note.</param>
    /// <param name="wishlists">The wishlists.</param>
    /// <returns><see langword="true"/> when every bound holds.</returns>
    public static bool AreMet(string? giftNote, IReadOnlyList<EventWishlist>? wishlists)
    {
        if (giftNote is { Length: > MaxGiftNoteLength })
        {
            return false;
        }

        var lists = wishlists ?? [];
        return lists.Count <= MaxEvents
            && lists.All(w => (w.Items ?? []).Count <= MaxWishlistItems
                && (w.Items ?? []).All(i => i is null || i.Length <= MaxWishlistItemLength));
    }
}
