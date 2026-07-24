using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Test.TestSupport;

namespace AppSuite.SecretSanta.Core.Test;

/// <summary>Tests for the <see cref="Group"/> aggregate invariants and its state memento.</summary>
public sealed class GroupTests
{
    private static readonly string[] _expectedWishlist = ["Scarf", "Book"];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyName(string name) =>
        Assert.Throws<ArgumentException>(() => Group.Create(name));

    [Fact]
    public void Create_TrimsNameAndAssignsId()
    {
        var group = Group.Create("  Family  ");

        Assert.Equal("Family", group.Name);
        Assert.NotEqual(Guid.Empty, group.Id);
    }

    [Fact]
    public void AddMember_RejectsDuplicateNames_CaseInsensitive()
    {
        var group = Group.Create("Friends");
        group.AddMember("Alice", Wishlist.Empty);

        Assert.Throws<InvalidOperationException>(() => group.AddMember("  alice ", Wishlist.Empty));
    }

    [Fact]
    public void AddExclusion_RejectsNonMemberEndpoints()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice", Wishlist.Empty);

        Assert.Throws<InvalidOperationException>(() => group.AddExclusion(alice, Guid.NewGuid()));
    }

    [Fact]
    public void RunDraw_WithFewerThanTwoMembers_Throws()
    {
        var group = Group.Create("Friends");
        group.AddMember("Alice", Wishlist.Empty);
        var generator = new DrawGenerator(new SeededRandomSource(1));

        Assert.Throws<InvalidOperationException>(() => group.RunDraw(generator, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RunDraw_AppendsCurrentDraw_OnSuccess()
    {
        var group = Group.Create("Friends");
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            group.AddMember(name, Wishlist.Empty);
        }

        var generator = new DrawGenerator(new SeededRandomSource(11));

        var result = group.RunDraw(generator, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.NotNull(group.CurrentDraw);
        Assert.Equal(4, group.CurrentDraw!.Assignments.Count);
    }

    [Fact]
    public void RemoveMember_VoidsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out var members);

        group.RemoveMember(members[0]);

        Assert.Null(group.CurrentDraw);
        Assert.True(group.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void AddMember_VoidsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out _);

        group.AddMember("Erin", Wishlist.Empty);

        Assert.Null(group.CurrentDraw);
        Assert.True(group.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void AddMember_BeforeAnyDraw_DoesNotFlagInvalidation()
    {
        var group = Group.Create("Friends");

        group.AddMember("Alice", Wishlist.Empty);

        Assert.False(group.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void EditMember_KeepsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out var members);

        group.EditMember(members[0], "Alicia", Wishlist.Create(["Socks"]));

        Assert.NotNull(group.CurrentDraw);
        Assert.False(group.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void RunDraw_AfterInvalidation_ClearsTheFlag()
    {
        var group = DrawnGroupOfFour(out var members);
        group.RemoveMember(members[0]);

        var result = group.RunDraw(new DrawGenerator(new SeededRandomSource(7)), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(group.DrawInvalidatedByMembershipChange);
        Assert.NotNull(group.CurrentDraw);
    }

    [Fact]
    public void ToState_FromState_RoundTripsInvalidationFlag()
    {
        var group = DrawnGroupOfFour(out var members);
        group.RemoveMember(members[0]);

        var restored = Group.FromState(group.ToState());

        Assert.True(restored.DrawInvalidatedByMembershipChange);
        Assert.Null(restored.CurrentDraw);
    }

    private static Group DrawnGroupOfFour(out IReadOnlyList<Guid> memberIds)
    {
        var group = Group.Create("Friends");
        var ids = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            ids.Add(group.AddMember(name, Wishlist.Empty));
        }

        group.RunDraw(new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);
        memberIds = ids;
        return group;
    }

    [Fact]
    public void ToState_FromState_RoundTrips()
    {
        var group = Group.Create("Family");
        group.SetBudget(Budget.Create(40m, "CHF"));
        group.SetEventDetails(EventDetails.Create(
            new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), "Grandma's", "Bring dessert"));
        var alice = group.AddMember("Alice", Wishlist.Create(["Scarf", "Book"]));
        var bob = group.AddMember("Bob", Wishlist.Empty);
        group.AddMember("Carol", Wishlist.Empty);
        group.AddMember("Dan", Wishlist.Empty);
        group.AddExclusion(alice, bob);
        group.RunDraw(new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);

        var restored = Group.FromState(group.ToState());

        Assert.Equal(group.Id, restored.Id);
        Assert.Equal(group.Name, restored.Name);
        Assert.Equal(group.Budget, restored.Budget);
        Assert.Equal(group.EventDetails, restored.EventDetails);
        Assert.Equal(group.Members.Count, restored.Members.Count);
        Assert.Equal(group.Exclusions, restored.Exclusions);
        Assert.NotNull(restored.CurrentDraw);
        Assert.Equal(group.CurrentDraw!.Assignments, restored.CurrentDraw!.Assignments);
        var restoredAlice = restored.FindMember(alice);
        Assert.NotNull(restoredAlice);
        Assert.Equal(_expectedWishlist, restoredAlice!.Wishlist.Items);
    }
}
