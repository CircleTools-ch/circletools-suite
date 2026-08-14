using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Tests for the <see cref="Group"/> aggregate invariants and its state memento (ADR-0042: events).</summary>
public sealed class GroupTests
{
    private static readonly string[] _expectedWishlist = ["Scarf", "Book"];

    // A group's first/default event takes the group's own id (ADR-0042), so tests address it by that.
    private static Event DefaultEvent(Group group) => group.FindEvent(group.Id)!;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyName(string name) =>
        Assert.Throws<ArgumentException>(() => Group.Create(name));

    [Fact]
    public void Create_TrimsNameAndAssignsId_AndHasOneDefaultEvent()
    {
        var group = Group.Create("  Family  ");

        Assert.Equal("Family", group.Name);
        Assert.NotEqual(Guid.Empty, group.Id);
        var occasion = Assert.Single(group.Events);
        Assert.Equal(group.Id, occasion.Id);
    }

    [Fact]
    public void AddMember_RejectsDuplicateNames_CaseInsensitive()
    {
        var group = Group.Create("Friends");
        group.AddMember("Alice");

        Assert.Throws<InvalidOperationException>(() => group.AddMember("  alice "));
    }

    [Fact]
    public void AddExclusion_RejectsNonMemberEndpoints()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");

        Assert.Throws<InvalidOperationException>(() => group.AddExclusion(alice, Guid.NewGuid()));
    }

    [Fact]
    public void RunDraw_WithFewerThanTwoMembers_Throws()
    {
        var group = Group.Create("Friends");
        group.AddMember("Alice");
        var generator = new DrawGenerator(new SeededRandomSource(1));

        Assert.Throws<InvalidOperationException>(() => group.RunDraw(group.Id, generator, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RunDraw_AppendsCurrentDraw_OnSuccess()
    {
        var group = Group.Create("Friends");
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            group.AddMember(name);
        }

        var generator = new DrawGenerator(new SeededRandomSource(11));

        var result = group.RunDraw(group.Id, generator, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.NotNull(DefaultEvent(group).CurrentDraw);
        Assert.Equal(4, DefaultEvent(group).CurrentDraw!.Assignments.Count);
    }

    [Fact]
    public void RemoveMember_VoidsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out var members);

        group.RemoveMember(members[0]);

        Assert.Null(DefaultEvent(group).CurrentDraw);
        Assert.True(DefaultEvent(group).DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void AddMember_VoidsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out _);

        group.AddMember("Erin");

        Assert.Null(DefaultEvent(group).CurrentDraw);
        Assert.True(DefaultEvent(group).DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void AddMember_BeforeAnyDraw_DoesNotFlagInvalidation()
    {
        var group = Group.Create("Friends");

        group.AddMember("Alice");

        Assert.False(DefaultEvent(group).DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void EditMember_KeepsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out var members);

        group.EditMember(members[0], "Alicia");

        Assert.NotNull(DefaultEvent(group).CurrentDraw);
        Assert.False(DefaultEvent(group).DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void RunDraw_AfterInvalidation_ClearsTheFlag()
    {
        var group = DrawnGroupOfFour(out var members);
        group.RemoveMember(members[0]);

        var result = group.RunDraw(group.Id, new DrawGenerator(new SeededRandomSource(7)), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(DefaultEvent(group).DrawInvalidatedByMembershipChange);
        Assert.NotNull(DefaultEvent(group).CurrentDraw);
    }

    [Fact]
    public void AddMember_VoidsEveryEventsDraw()
    {
        var group = DrawnGroupOfFour(out _);
        var second = group.AddEvent("Birthday");
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(3)), DateTimeOffset.UtcNow);
        Assert.NotNull(group.FindEvent(second)!.CurrentDraw);

        group.AddMember("Erin");

        // A roster change voids the draw of every event, not just one.
        Assert.Null(DefaultEvent(group).CurrentDraw);
        Assert.Null(group.FindEvent(second)!.CurrentDraw);
        Assert.True(group.FindEvent(second)!.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void EventsAreIndependent_ReDrawingOneLeavesAnotherAlone()
    {
        var group = DrawnGroupOfFour(out _);
        var second = group.AddEvent("Birthday");
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(3)), DateTimeOffset.UtcNow);
        var firstDrawId = DefaultEvent(group).CurrentDraw!.Id;

        // Re-draw only the second event.
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(99)), DateTimeOffset.UtcNow);

        Assert.Equal(firstDrawId, DefaultEvent(group).CurrentDraw!.Id);
    }

    [Fact]
    public void RemoveEvent_RejectsRemovingTheLastEvent()
    {
        var group = Group.Create("Friends");

        Assert.Throws<InvalidOperationException>(() => group.RemoveEvent(group.Id));
    }

    [Fact]
    public void Wishlists_ArePerEvent()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");
        var second = group.AddEvent("Birthday");

        group.SetMemberWishlist(group.Id, alice, Wishlist.Create(["Scarf"]));
        group.SetMemberWishlist(second, alice, Wishlist.Create(["Bike"]));

        Assert.Equal(["Scarf"], DefaultEvent(group).WishlistFor(alice).Items);
        Assert.Equal(["Bike"], group.FindEvent(second)!.WishlistFor(alice).Items);
    }

    [Fact]
    public void ToState_FromState_RoundTripsInvalidationFlag()
    {
        var group = DrawnGroupOfFour(out var members);
        group.RemoveMember(members[0]);

        var restored = Group.FromState(group.ToState());

        Assert.True(DefaultEvent(restored).DrawInvalidatedByMembershipChange);
        Assert.Null(DefaultEvent(restored).CurrentDraw);
    }

    private static Group DrawnGroupOfFour(out IReadOnlyList<Guid> memberIds)
    {
        var group = Group.Create("Friends");
        var ids = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            ids.Add(group.AddMember(name));
        }

        group.RunDraw(group.Id, new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);
        memberIds = ids;
        return group;
    }

    [Fact]
    public void ToState_FromState_RoundTrips()
    {
        var group = Group.Create("Family");
        group.SetEventBudget(group.Id, Budget.Create(40m, "CHF"));
        group.SetEventDetails(group.Id, EventDetails.Create(
            new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), "Grandma's", "Bring dessert"));
        var alice = group.AddMember("Alice");
        var bob = group.AddMember("Bob");
        group.AddMember("Carol");
        group.AddMember("Dan");
        group.SetMemberWishlist(group.Id, alice, Wishlist.Create(["Scarf", "Book"]));
        group.AddExclusion(alice, bob);
        group.RunDraw(group.Id, new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);

        var restored = Group.FromState(group.ToState());

        Assert.Equal(group.Id, restored.Id);
        Assert.Equal(group.Name, restored.Name);
        Assert.Equal(DefaultEvent(group).Budget, DefaultEvent(restored).Budget);
        Assert.Equal(DefaultEvent(group).EventDetails, DefaultEvent(restored).EventDetails);
        Assert.Equal(group.Members.Count, restored.Members.Count);
        Assert.Equal(group.Exclusions, restored.Exclusions);
        Assert.NotNull(DefaultEvent(restored).CurrentDraw);
        Assert.Equal(DefaultEvent(group).CurrentDraw!.Assignments, DefaultEvent(restored).CurrentDraw!.Assignments);
        Assert.Equal(_expectedWishlist, DefaultEvent(restored).WishlistFor(alice).Items);
    }
}
