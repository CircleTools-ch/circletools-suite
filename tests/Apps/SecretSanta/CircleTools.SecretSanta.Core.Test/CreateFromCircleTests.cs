using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Tests for composing a new group from a circle snapshot (FR-027). The mapping is the unit under test: every
/// chosen person becomes a member with its origin recorded; an accounted person's account is paired with the
/// member for the caller to make a participant, and never written into the group (ADR-0086); when two chosen
/// people resolve to the same account exactly one is paired and the rest are reported.
/// </summary>
public sealed class CreateFromCircleTests
{
    [Fact]
    public async Task CreatesOneMemberPerChosenPerson_AndPairsTheAccountedOnesWithTheirAccount()
    {
        var repository = new InMemoryGroupRepository();
        var aliceUser = Guid.NewGuid();
        var alicePerson = Guid.NewGuid();
        var bobPerson = Guid.NewGuid();

        var result = await Handle(
            repository,
            "December 2026",
            new CirclePersonInput(alicePerson, "Alice", aliceUser),  // has an account
            new CirclePersonInput(bobPerson, "Bob (managed)", UserId: null)); // managed → no account

        var group = await repository.GetAsync(result.GroupId, default);
        Assert.NotNull(group);
        Assert.Equal(2, group!.Members.Count);
        Assert.Empty(result.BroughtInUnlinkedNames);

        var alice = group.Members.Single(m => m.Name == "Alice");
        Assert.Equal(alicePerson, alice.OriginCirclePersonId);
        Assert.Equal(new CircleMemberAccount(alice.Id, aliceUser), Assert.Single(result.Accounts));

        var bob = group.Members.Single(m => m.Name == "Bob (managed)");
        Assert.Equal(bobPerson, bob.OriginCirclePersonId);

        // The document carries no account: a restored copy knows the origin only.
        Assert.DoesNotContain(aliceUser.ToString(), CircleTools.SecretSanta.Core.Persistence.GroupDocument.Serialize(group.ToState()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TwoPeopleResolvingToTheSameAccount_PairOnlyOne_AndTheUserIsInformed()
    {
        var repository = new InMemoryGroupRepository();
        var sharedUser = Guid.NewGuid();

        var result = await Handle(
            repository,
            "Family",
            new CirclePersonInput(Guid.NewGuid(), "Alice", sharedUser),
            new CirclePersonInput(Guid.NewGuid(), "Alice's second entry", sharedUser));

        var group = await repository.GetAsync(result.GroupId, default);
        var paired = Assert.Single(result.Accounts);

        Assert.Equal("Alice", group!.FindMember(paired.MemberId)!.Name);
        Assert.Equal(["Alice's second entry"], result.BroughtInUnlinkedNames);
    }

    [Fact]
    public async Task TheNewGroup_CarriesNoDrawUntilRun()
    {
        var repository = new InMemoryGroupRepository();

        var result = await Handle(
            repository,
            "Friends",
            new CirclePersonInput(Guid.NewGuid(), "Alice", Guid.NewGuid()),
            new CirclePersonInput(Guid.NewGuid(), "Bob", null));

        // A group created from a circle starts eventless like any other (ADR-0042 amendment): the
        // members come across, but the first occasion is created by the user, so there is no draw.
        var group = await repository.GetAsync(result.GroupId, default);
        Assert.Empty(group!.Events);
    }

    private static Task<CreateGroupFromCircleResult> Handle(
        InMemoryGroupRepository repository,
        string name,
        params CirclePersonInput[] people) =>
        new CreateGroupFromCircleCommandHandler(repository)
            .HandleAsync(new CreateGroupFromCircleCommand(name, people), default);
}
