using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Tests for composing a new group from a circle snapshot (FR-027). The mapping is the unit under
/// test: accounted person → linked member; managed member / no account → unlinked; the origin id is
/// recorded; and when two chosen people resolve to the same account exactly one is linked and the
/// rest are reported so the UI can tell the user.
/// </summary>
public sealed class CreateFromCircleTests
{
    [Fact]
    public async Task CreatesOneMemberPerChosenPerson_LinkedWhereAccounted_UnlinkedOtherwise()
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
        Assert.Equal(aliceUser, alice.UserId);
        Assert.Equal(alicePerson, alice.OriginCirclePersonId);
        Assert.True(alice.IsLinked);

        var bob = group.Members.Single(m => m.Name == "Bob (managed)");
        Assert.Null(bob.UserId);
        Assert.Equal(bobPerson, bob.OriginCirclePersonId);
        Assert.False(bob.IsLinked);
    }

    [Fact]
    public async Task TwoPeopleResolvingToTheSameAccount_LinkOnlyOne_AndTheUserIsInformed()
    {
        var repository = new InMemoryGroupRepository();
        var sharedUser = Guid.NewGuid();

        var result = await Handle(
            repository,
            "Family",
            new CirclePersonInput(Guid.NewGuid(), "Alice", sharedUser),
            new CirclePersonInput(Guid.NewGuid(), "Alice's second entry", sharedUser));

        var group = await repository.GetAsync(result.GroupId, default);
        var linked = group!.Members.Where(m => m.IsLinked).ToList();

        Assert.Single(linked);
        Assert.Equal("Alice", linked[0].Name);
        Assert.Equal(["Alice's second entry"], result.BroughtInUnlinkedNames);
    }

    [Fact]
    public async Task TheNewGroup_IsAccountScopedAndCarriesNoDrawUntilRun()
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
