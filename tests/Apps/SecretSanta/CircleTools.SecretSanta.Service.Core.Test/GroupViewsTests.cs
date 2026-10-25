using System.Text.Json;
using CircleTools.SecretSanta.Service.Core.Views;

namespace CircleTools.SecretSanta.Service.Core.Test;

/// <summary>
/// The views the server derives from a group document (ADR-0086). The privacy rule is the first assertion: a
/// view carries the reader's own pairing and no other (NFR-002, ADR-0020).
/// </summary>
public sealed class GroupViewsTests
{
    [Fact]
    public void AParticipantView_CarriesOnlyTheirOwnPairing()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var anna = members["Anna"];
        var recipient = TestGroups.RecipientOf(document, eventId, anna);

        var view = GroupViews.ForParticipant(document, 3, anna, ["readOwnView"])!;

        var occasion = Assert.Single(view.Events);
        Assert.Equal(TestGroups.NameOf(document, recipient), occasion.Assignment!.RecipientName);

        // Nobody else's recipient may appear anywhere in the serialized view.
        var json = JsonSerializer.Serialize(view);
        var othersRecipients = members.Values
            .Where(m => m != anna)
            .Select(giver => TestGroups.RecipientOf(document, eventId, giver))
            .Where(r => r != recipient && r != anna)
            .Select(r => TestGroups.NameOf(document, r));
        foreach (var name in othersRecipients)
        {
            Assert.DoesNotContain($"\"n\":\"{name}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AParticipantView_ShowsTheRecipientsWishesAndTheirOwnEntry()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var giverOfBen = members.Values.Single(m => TestGroups.RecipientOf(document, eventId, m) == members["Ben"]);

        var giverView = GroupViews.ForParticipant(document, 1, giverOfBen, [])!;
        var bensView = GroupViews.ForParticipant(document, 1, members["Ben"], [])!;

        Assert.Equal(["Scarf", "Tea"], giverView.Events[0].Assignment!.Wishlist);
        Assert.Equal(["Scarf", "Tea"], bensView.Events[0].Wishlist);
        Assert.Equal("Christmas 2026", bensView.Events[0].EventName);
    }

    [Fact]
    public void BeforeTheDraw_TheViewHasNoPairing()
    {
        var (document, _, memberId) = TestGroups.Undrawn();

        var view = GroupViews.ForParticipant(document, 1, memberId, [])!;

        Assert.Null(Assert.Single(view.Events).Assignment);
    }

    [Fact]
    public void AMemberNoLongerOnTheRoster_GetsNoView()
    {
        var (document, _, _) = TestGroups.Undrawn();

        Assert.Null(GroupViews.ForParticipant(document, 1, Guid.NewGuid(), []));
    }

    [Fact]
    public void TheRevealPage_IsTheGiversOwnPairing()
    {
        var (document, eventId, members) = TestGroups.Drawn();
        var dan = members["Dan"];

        var page = GroupViews.ForReveal(document, eventId, dan)!;

        Assert.Equal(TestGroups.NameOf(document, TestGroups.RecipientOf(document, eventId, dan)), page.RecipientName);
        Assert.Equal("Family", page.GroupName);
        Assert.Null(GroupViews.ForReveal(document, Guid.NewGuid(), dan));
    }

    [Fact]
    public void TheJoinPreview_MarksTakenMembers()
    {
        var (document, _, members) = TestGroups.Drawn();

        var preview = GroupViews.ForJoin(document, new HashSet<Guid> { members["Cleo"] });

        Assert.Equal(4, preview.Members.Count);
        Assert.True(preview.Members.Single(m => m.Name == "Cleo").Taken);
        Assert.False(preview.Members.Single(m => m.Name == "Anna").Taken);
    }
}
