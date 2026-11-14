// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Entries;

namespace CircleTools.SecretSanta.Service.Core.Test;

/// <summary>
/// A member's own entry merged into the group document (ADR-0086): it replaces that member's gift note and the
/// wishlists it names, and nothing else.
/// </summary>
public sealed class MemberEntryMergeTests
{
    [Fact]
    public void AnEntry_SetsTheGiftNoteAndTheNamedWishlists()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var entry = new MemberEntry(members["Anna"], "No sweets", [new EventWishlist(eventId, ["Book", " ", "Socks"])], MemberEntryKeeper.Participant);

        var (merged, changed) = MemberEntryMerge.Apply(document, [entry]);

        Assert.True(changed);
        var group = Group.FromState(merged);
        Assert.Equal("No sweets", group.FindMember(members["Anna"])!.GiftNote);
        Assert.Equal(["Book", "Socks"], group.FindEvent(eventId)!.WishlistFor(members["Anna"]).Items);

        // Ben's wishlist and the draw are untouched.
        Assert.Equal(["Scarf", "Tea"], group.FindEvent(eventId)!.WishlistFor(members["Ben"]).Items);
        Assert.Equal(
            Group.FromState(document).FindEvent(eventId)!.CurrentDraw!.Id,
            group.FindEvent(eventId)!.CurrentDraw!.Id);
    }

    [Fact]
    public void ABlankGiftNoteAndAnEmptyWishlist_ClearThem()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var entry = new MemberEntry(members["Ben"], "  ", [new EventWishlist(eventId, [])], MemberEntryKeeper.RevealLink);

        var (merged, _) = MemberEntryMerge.Apply(document, [entry]);

        var group = Group.FromState(merged);
        Assert.Equal(string.Empty, group.FindMember(members["Ben"])!.GiftNote);
        Assert.Empty(group.FindEvent(eventId)!.WishlistFor(members["Ben"]).Items);
    }

    [Fact]
    public void AnEntryOfARemovedMember_IsSkipped_SoTheirDataStaysDeleted()
    {
        // Anna is in the draw, so removing her archives the name only (ADR-0052 amendment); her entry must not
        // bring her wishlist and note back.
        var (drawn, eventId, members) = TestGroups.Drawn();
        var group = Group.FromState(drawn);
        group.RemoveMember(members["Anna"]);
        var document = group.ToState();
        var entry = new MemberEntry(members["Anna"], "No sweets", [new EventWishlist(eventId, ["Book"])], MemberEntryKeeper.Participant);

        var (merged, changed) = MemberEntryMerge.Apply(document, [entry]);

        Assert.False(changed);
        Assert.Same(document, merged);
    }

    [Fact]
    public void UnknownMembersAndEvents_AreSkipped()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var entries = new[]
        {
            new MemberEntry(Guid.NewGuid(), "ghost", [new EventWishlist(eventId, ["x"])], MemberEntryKeeper.Participant),
            new MemberEntry(members["Cleo"], null, [new EventWishlist(Guid.NewGuid(), ["y"])], MemberEntryKeeper.Participant),
        };

        var (merged, changed) = MemberEntryMerge.Apply(document, entries);

        Assert.False(changed);
        Assert.Same(document, merged);
    }

    [Fact]
    public void ReapplyingTheSameEntry_ChangesNothing()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var entry = new MemberEntry(members["Dan"], "Vegan", [new EventWishlist(eventId, ["Plant"])], MemberEntryKeeper.Participant);
        var (once, _) = MemberEntryMerge.Apply(document, [entry]);

        var (twice, changed) = MemberEntryMerge.Apply(once, [entry]);

        Assert.False(changed);
        Assert.Equal(GroupDocument.Serialize(once), GroupDocument.Serialize(twice));
    }

    [Theory]
    [InlineData(501, 1, 1, false)]
    [InlineData(500, 51, 1, false)]
    [InlineData(500, 50, 101, false)]
    [InlineData(500, 50, 100, true)]
    public void Limits_BoundAnEntry(int noteLength, int events, int items, bool ok)
    {
        var wishlists = Enumerable.Range(0, events)
            .Select(_ => new EventWishlist(Guid.NewGuid(), [.. Enumerable.Repeat("x", items)]))
            .ToList();

        Assert.Equal(ok, MemberEntryLimits.AreMet(new string('n', noteLength), wishlists));
    }
}
