using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Test.TestSupport;

namespace AppSuite.SecretSanta.Core.Test;

/// <summary>
/// Domain-level tests for member↔account linking (M4). The load-bearing rule here is that linking or
/// unlinking changes how a member is <em>served</em>, not the participant set, and so must not
/// invalidate a completed draw — unlike adding or removing a member.
/// </summary>
public sealed class MemberLinkingDomainTests
{
    [Fact]
    public void LinkMember_DoesNotInvalidateACompletedDraw()
    {
        var group = DrawnGroupOfFour(out var members);

        group.LinkMember(members[0], Guid.NewGuid());

        Assert.NotNull(group.CurrentDraw);
        Assert.False(group.DrawInvalidatedByMembershipChange);
    }

    [Fact]
    public void UnlinkMember_DoesNotInvalidateACompletedDraw()
    {
        var group = DrawnGroupOfFour(out var members);
        var user = Guid.NewGuid();
        group.LinkMember(members[0], user);

        group.UnlinkMember(members[0]);

        Assert.NotNull(group.CurrentDraw);
        Assert.False(group.DrawInvalidatedByMembershipChange);
        Assert.False(group.FindMember(members[0])!.IsLinked);
    }

    [Fact]
    public void LinkMember_SetsUserId_AndIsLinked()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice", Wishlist.Empty);
        var user = Guid.NewGuid();

        group.LinkMember(alice, user);

        var member = group.FindMember(alice)!;
        Assert.True(member.IsLinked);
        Assert.Equal(user, member.UserId);
    }

    [Fact]
    public void LinkMember_WhenUserAlreadyBacksAnotherMember_Throws()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice", Wishlist.Empty);
        var bob = group.AddMember("Bob", Wishlist.Empty);
        var user = Guid.NewGuid();
        group.LinkMember(alice, user);

        // One user backs at most one member per group.
        Assert.Throws<InvalidOperationException>(() => group.LinkMember(bob, user));
    }

    [Fact]
    public void LinkMember_ReLinkingSameUserToSameMember_IsAllowed()
    {
        var group = Group.Create("Friends");
        var alice = group.AddMember("Alice", Wishlist.Empty);
        var user = Guid.NewGuid();
        group.LinkMember(alice, user);

        // Idempotent re-application of the same resolution must not trip the uniqueness guard.
        group.LinkMember(alice, user);

        Assert.Equal(user, group.FindMember(alice)!.UserId);
    }

    [Fact]
    public void UnlinkMember_UnknownMember_Throws()
    {
        var group = Group.Create("Friends");

        Assert.Throws<InvalidOperationException>(() => group.UnlinkMember(Guid.NewGuid()));
    }

    [Fact]
    public void ToState_FromState_RoundTripsLinkAndOrigin()
    {
        var group = Group.Create("Family");
        var origin = Guid.NewGuid();
        var user = Guid.NewGuid();
        var alice = group.AddMemberFromCircle("Alice", Wishlist.Create(["Scarf"]), user, origin);
        group.AddMemberFromCircle("Bob", Wishlist.Empty, userId: null, originCirclePersonId: Guid.NewGuid());

        var restored = Group.FromState(group.ToState());

        var restoredAlice = restored.FindMember(alice)!;
        Assert.Equal(user, restoredAlice.UserId);
        Assert.Equal(origin, restoredAlice.OriginCirclePersonId);
        Assert.True(restoredAlice.IsLinked);
    }

    [Fact]
    public void MemberState_WrittenBeforeLinkingExisted_DeserializesAsUnlinked()
    {
        // Older snapshots used the three-argument MemberState; the new fields default to null.
        var group = Group.Create("Legacy");
        var alice = group.AddMember("Alice", Wishlist.Empty);

        var restored = Group.FromState(group.ToState());

        var restoredAlice = restored.FindMember(alice)!;
        Assert.Null(restoredAlice.UserId);
        Assert.Null(restoredAlice.OriginCirclePersonId);
        Assert.False(restoredAlice.IsLinked);
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
}
