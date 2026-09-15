using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Domain tests for carrying a new occasion forward from a previous one (FR-035 / UC-SS-014): copy
/// wishlists and budget, seed the no-repeat draw baseline (so the next draw avoids last time's pairings),
/// and the graceful-failure escape when that over-constrains a small roster.
/// </summary>
public sealed class CarryForwardTests
{
    private static readonly DateTimeOffset _now = new(2026, 12, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddEventFrom_CopiesWishlistsAndBudget_AndStaysUndrawn()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.AddMember("Bob");
        group.AddMember("Carol");
        var source = group.AddEvent("Christmas 2026");
        group.SetEventBudget(source, Budget.Create(40m, "CHF"));
        group.SetMemberWishlist(source, alice, Wishlist.Create(["Scarf", "Book"]));

        var next = group.AddEventFrom("Christmas 2027", source, _now);

        var occasion = group.FindEvent(next)!;
        Assert.Equal(Budget.Create(40m, "CHF"), occasion.Budget);
        Assert.Equal(["Scarf", "Book"], occasion.WishlistFor(alice).Items);
        Assert.Null(occasion.CurrentDraw);           // undrawn — no reveal links
        Assert.Empty(occasion.Draws);

        // The source is untouched.
        Assert.Equal(["Scarf", "Book"], group.FindEvent(source)!.WishlistFor(alice).Items);
    }

    [Fact]
    public void AddEventFrom_SeedsBaseline_SoNextDrawAvoidsSourcePairings()
    {
        var group = FourMemberGroupWithDrawnSource(out var source);
        var sourceMap = group.FindEvent(source)!.CurrentDraw!.ToMap();

        var next = group.AddEventFrom("Next", source, _now);
        var result = group.RunDraw(next, new DrawGenerator(new SeededRandomSource(11)), _now);

        Assert.True(result.IsSuccess);
        var nextMap = group.FindEvent(next)!.CurrentDraw!.ToMap();
        Assert.NotEqual(sourceMap, nextMap); // a different set of pairings than last time
    }

    [Fact]
    public void AddEventFrom_DoesNotCopyAnArchivedMembersWishlist()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.AddMember("Bob");
        group.AddMember("Carol");
        var source = group.AddEvent("Christmas 2026");
        group.SetMemberWishlist(source, alice, Wishlist.Create(["Scarf"]));
        group.RemoveMember(alice); // archived after the source occasion

        var next = group.AddEventFrom("Christmas 2027", source, _now);

        // Alice is history; her wishlist does not carry into the new occasion.
        Assert.Empty(group.FindEvent(next)!.WishlistMemberIds);
    }

    [Fact]
    public void AddEventFrom_LeavesAMemberAbsentFromTheSource_WithAnEmptyWishlist()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.AddMember("Bob");
        group.AddMember("Carol");
        var source = group.AddEvent("Christmas 2026");
        group.SetMemberWishlist(source, alice, Wishlist.Create(["Scarf"]));
        var dan = group.AddMember("Dan"); // joins after the source occasion had wishlists

        var next = group.AddEventFrom("Christmas 2027", source, _now);

        Assert.Equal(["Scarf"], group.FindEvent(next)!.WishlistFor(alice).Items);
        Assert.Empty(group.FindEvent(next)!.WishlistFor(dan).Items);
    }

    [Fact]
    public void AddEventFrom_Baseline_RoundTripsThroughState()
    {
        var group = FourMemberGroupWithDrawnSource(out var source);
        var next = group.AddEventFrom("Next", source, _now);

        var restored = Group.FromState(group.ToState());

        // The restored (still undrawn) event refuses to reproduce the carried baseline on its first draw.
        var sourceMap = restored.FindEvent(source)!.CurrentDraw!.ToMap();
        var result = restored.RunDraw(next, new DrawGenerator(new SeededRandomSource(11)), _now);
        Assert.True(result.IsSuccess);
        Assert.NotEqual(sourceMap, restored.FindEvent(next)!.CurrentDraw!.ToMap());
    }

    [Fact]
    public void RunDraw_OverConstrainedByBaseline_FailsGracefully_ThenSucceedsWhenIgnored()
    {
        // Three members have exactly two possible derangements. Forbid one of them, so only the carried
        // baseline remains valid — then rule (c) makes the draw impossible until the baseline is ignored.
        var group = Group.Create("Trio");
        var a = group.AddMember("A");
        var b = group.AddMember("B");
        var c = group.AddMember("C");
        var source = group.AddEvent("Source");
        group.RunDraw(source, new DrawGenerator(new SeededRandomSource(1)), _now);

        var next = group.AddEventFrom("Next", source, _now);

        // Kill the *other* derangement by excluding one of its edges, leaving only the baseline valid.
        var baseline = group.FindEvent(next)!;
        var baselineMap = group.FindEvent(source)!.CurrentDraw!.ToMap();
        var otherRecipientForA = new[] { b, c }.Single(id => id != baselineMap[a]);
        group.AddExclusion(a, otherRecipientForA);

        var blocked = group.RunDraw(next, new DrawGenerator(new SeededRandomSource(5)), _now);
        Assert.False(blocked.IsSuccess); // graceful failure — no partial result

        var ignored = group.RunDraw(next, new DrawGenerator(new SeededRandomSource(5)), _now, ignoreCarriedBaseline: true);
        Assert.True(ignored.IsSuccess);
        _ = baseline;
    }

    [Fact]
    public void AddEventFrom_UnknownSource_Throws()
    {
        var group = Group.Create("Family");
        group.AddMember("Alice");

        Assert.Throws<InvalidOperationException>(() => group.AddEventFrom("Next", Guid.NewGuid(), _now));
    }

    private static Group FourMemberGroupWithDrawnSource(out Guid source)
    {
        var group = Group.Create("Family");
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            group.AddMember(name);
        }

        source = group.AddEvent("Christmas 2026");
        group.RunDraw(source, new DrawGenerator(new SeededRandomSource(11)), _now);
        return group;
    }
}
