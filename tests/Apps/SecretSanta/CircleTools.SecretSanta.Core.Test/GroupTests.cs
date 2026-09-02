using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Tests for the <see cref="Group"/> aggregate invariants and its state memento (ADR-0042: events).</summary>
/// <remarks>
/// A new group starts with <strong>no</strong> event (ADR-0042 amendment, 2026-09-01), so tests that
/// need an occasion add one and address it by the returned id.
/// </remarks>
public sealed class GroupTests
{
    private static readonly string[] _expectedWishlist = ["Scarf", "Book"];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyName(string name) =>
        Assert.Throws<ArgumentException>(() => Group.Create(name));

    [Fact]
    public void Create_TrimsNameAndAssignsId_AndHasNoEvent()
    {
        var group = Group.Create("  Family  ");

        Assert.Equal("Family", group.Name);
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Empty(group.Events);
    }

    [Fact]
    public void AddMember_RejectsDuplicateNames_CaseInsensitive()
    {
        var group = Group.Create("Friends");
        group.AddMember("Alice");

        Assert.Throws<DuplicateMemberNameException>(() => group.AddMember("  alice "));
    }

    [Fact]
    public void AddExclusion_RejectsNonMemberEndpoints()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");

        Assert.Throws<InvalidOperationException>(() => group.AddExclusion(alice, Guid.NewGuid()));
    }

    [Fact]
    public void AddExclusion_IsOneWayByDefault()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");
        var bob = group.AddMember("Bob");

        group.AddExclusion(alice, bob);

        Assert.False(Assert.Single(group.Exclusions).Mutual);
    }

    [Fact]
    public void AddExclusion_ReplacesExistingRuleForSamePair()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");
        var bob = group.AddMember("Bob");

        group.AddExclusion(alice, bob);                 // one-way
        group.AddExclusion(bob, alice, mutual: true);   // reconfigure the same pair as mutual

        Assert.True(Assert.Single(group.Exclusions).Mutual);
    }

    [Fact]
    public void MutualExclusion_IsOrderIndependent()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.Equal(Exclusion.Create(a, b, mutual: true), Exclusion.Create(b, a, mutual: true));
    }

    [Fact]
    public void ToState_RoundTrips_MutualFlag()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");
        var bob = group.AddMember("Bob");
        group.AddExclusion(alice, bob, mutual: true);

        var restored = Group.FromState(group.ToState());

        Assert.True(Assert.Single(restored.Exclusions).Mutual);
    }

    [Fact]
    public void ToState_FromState_KeepsAnEventlessGroupEventless()
    {
        var group = Group.Create("Friends");

        var restored = Group.FromState(group.ToState());

        Assert.Empty(restored.Events);
    }

    [Fact]
    public void RunDraw_WithFewerThanTwoMembers_Throws()
    {
        var group = Group.Create("Friends");
        var eventId = group.AddEvent("Christmas");
        group.AddMember("Alice");
        var generator = new DrawGenerator(new SeededRandomSource(1));

        Assert.Throws<InvalidOperationException>(() => group.RunDraw(eventId, generator, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RunDraw_AppendsCurrentDraw_OnSuccess()
    {
        var group = Group.Create("Friends");
        var eventId = group.AddEvent("Christmas");
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            group.AddMember(name);
        }

        var generator = new DrawGenerator(new SeededRandomSource(11));

        var result = group.RunDraw(eventId, generator, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
        Assert.Equal(4, group.FindEvent(eventId)!.CurrentDraw!.Assignments.Count);
    }

    [Fact]
    public void RemoveMember_ArchivesAndMarksDrawStale_KeepingHistory()
    {
        var group = DrawnGroupOfFour(out var members, out var eventId);

        group.RemoveMember(members[0]);

        // The completed draw is kept as history, only flagged as no longer matching the roster (ADR-0052).
        var occasion = group.FindEvent(eventId)!;
        Assert.NotNull(occasion.CurrentDraw);
        Assert.True(occasion.DrawInvalidatedByMembershipChange);

        // The member is archived: gone from the active roster, still resolvable so the past draw reads.
        Assert.Equal(3, group.Members.Count);
        Assert.DoesNotContain(group.Members, m => m.Id == members[0]);
        Assert.True(group.FindMember(members[0])!.IsArchived);
        Assert.Equal("A", group.FindMember(members[0])!.Name);
    }

    [Fact]
    public void EditMember_RenameIsVisibleForPastDraws()
    {
        var group = DrawnGroupOfFour(out var members, out var eventId);

        group.EditMember(members[0], "Renamed");

        // A past draw resolves names through FindMember, so a rename reaches it too.
        Assert.Equal("Renamed", group.FindMember(members[0])!.Name);
        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
    }

    [Fact]
    public void AddMember_MarksDrawStale_ButKeepsIt()
    {
        var group = DrawnGroupOfFour(out _, out var eventId);

        group.AddMember("Erin");

        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
        Assert.True(group.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void AddMember_BeforeAnyDraw_DoesNotFlagInvalidation()
    {
        var group = Group.Create("Friends");
        var eventId = group.AddEvent("Christmas");

        group.AddMember("Alice");

        Assert.False(group.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void EditMember_KeepsCurrentDraw()
    {
        var group = DrawnGroupOfFour(out var members, out var eventId);

        group.EditMember(members[0], "Alicia");

        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
        Assert.False(group.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void RunDraw_AfterInvalidation_ClearsTheFlag()
    {
        var group = DrawnGroupOfFour(out var members, out var eventId);
        group.RemoveMember(members[0]);

        var result = group.RunDraw(eventId, new DrawGenerator(new SeededRandomSource(7)), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(group.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
    }

    [Fact]
    public void AddMember_MarksEveryEventsDrawStale_ButKeepsThem()
    {
        var group = DrawnGroupOfFour(out _, out var eventId);
        var second = group.AddEvent("Birthday");
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(3)), DateTimeOffset.UtcNow);
        Assert.NotNull(group.FindEvent(second)!.CurrentDraw);

        group.AddMember("Erin");

        // A roster change flags every event's draw (not just one), but keeps them all (ADR-0052).
        Assert.NotNull(group.FindEvent(eventId)!.CurrentDraw);
        Assert.NotNull(group.FindEvent(second)!.CurrentDraw);
        Assert.True(group.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
        Assert.True(group.FindEvent(second)!.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void EventsAreIndependent_ReDrawingOneLeavesAnotherAlone()
    {
        var group = DrawnGroupOfFour(out _, out var eventId);
        var second = group.AddEvent("Birthday");
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(3)), DateTimeOffset.UtcNow);
        var firstDrawId = group.FindEvent(eventId)!.CurrentDraw!.Id;

        // Re-draw only the second event.
        group.RunDraw(second, new DrawGenerator(new SeededRandomSource(99)), DateTimeOffset.UtcNow);

        Assert.Equal(firstDrawId, group.FindEvent(eventId)!.CurrentDraw!.Id);
    }

    [Fact]
    public void RemoveEvent_CanRemoveTheOnlyEvent()
    {
        var group = Group.Create("Friends");
        var eventId = group.AddEvent("Christmas");

        group.RemoveEvent(eventId);

        Assert.Empty(group.Events);
    }

    [Fact]
    public void Wishlists_ArePerEvent()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice");
        var first = group.AddEvent("Christmas");
        var second = group.AddEvent("Birthday");

        group.SetMemberWishlist(first, alice, Wishlist.Create(["Scarf"]));
        group.SetMemberWishlist(second, alice, Wishlist.Create(["Bike"]));

        Assert.Equal(["Scarf"], group.FindEvent(first)!.WishlistFor(alice).Items);
        Assert.Equal(["Bike"], group.FindEvent(second)!.WishlistFor(alice).Items);
    }

    [Fact]
    public void ToState_FromState_RoundTripsInvalidationFlag_AndArchivedMember()
    {
        var group = DrawnGroupOfFour(out var members, out var eventId);
        group.RemoveMember(members[0]);

        var restored = Group.FromState(group.ToState());

        Assert.True(restored.FindEvent(eventId)!.DrawInvalidatedByMembershipChange);
        Assert.NotNull(restored.FindEvent(eventId)!.CurrentDraw); // draw kept as history

        // The archived member survives the round-trip: absent from the roster, present for resolution.
        Assert.Equal(3, restored.Members.Count);
        Assert.True(restored.FindMember(members[0])!.IsArchived);
    }

    private static Group DrawnGroupOfFour(out IReadOnlyList<Guid> memberIds, out Guid eventId)
    {
        var group = Group.Create("Friends");
        eventId = group.AddEvent("Christmas");
        var ids = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            ids.Add(group.AddMember(name));
        }

        group.RunDraw(eventId, new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);
        memberIds = ids;
        return group;
    }

    [Fact]
    public void ToState_FromState_RoundTrips()
    {
        var group = Group.Create("Family");
        var eventId = group.AddEvent("Christmas");
        group.SetEventBudget(eventId, Budget.Create(40m, "CHF"));
        group.SetEventDetails(eventId, EventDetails.Create(
            new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), "Grandma's", "Bring dessert"));
        var alice = group.AddMember("Alice");
        var bob = group.AddMember("Bob");
        group.AddMember("Carol");
        group.AddMember("Dan");
        group.SetMemberWishlist(eventId, alice, Wishlist.Create(["Scarf", "Book"]));
        group.AddExclusion(alice, bob);
        group.RunDraw(eventId, new DrawGenerator(new SeededRandomSource(11)), DateTimeOffset.UtcNow);

        var restored = Group.FromState(group.ToState());

        Assert.Equal(group.Id, restored.Id);
        Assert.Equal(group.Name, restored.Name);
        Assert.Equal(group.FindEvent(eventId)!.Budget, restored.FindEvent(eventId)!.Budget);
        Assert.Equal(group.FindEvent(eventId)!.EventDetails, restored.FindEvent(eventId)!.EventDetails);
        Assert.Equal(group.Members.Count, restored.Members.Count);
        Assert.Equal(group.Exclusions, restored.Exclusions);
        Assert.NotNull(restored.FindEvent(eventId)!.CurrentDraw);
        Assert.Equal(group.FindEvent(eventId)!.CurrentDraw!.Assignments, restored.FindEvent(eventId)!.CurrentDraw!.Assignments);
        Assert.Equal(_expectedWishlist, restored.FindEvent(eventId)!.WishlistFor(alice).Items);
    }
}
