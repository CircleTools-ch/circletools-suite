using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Core.Reveal;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Tests exercising the application handlers over an in-memory repository (ADR-0042: events).</summary>
public sealed class HandlerTests
{
    private static readonly string[] _expectedLabels = ["Alice", "Bob", "Carol", "Dan"];

    [Fact]
    public async Task RunDraw_Then_GetRevealLinks_ProducesOneDecodableLinkPerMember()
    {
        var repository = new InMemoryGroupRepository();
        var generator = new DrawGenerator(new SeededRandomSource(123));
        var codec = new RevealLinkCodec();
        var linkBuilder = new RevealLinkBuilder(new RevealLinkOptions("https://santa.example"), codec);

        // Arrange a group with an event, four members, and a budget on that event.
        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var eventId = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        await new SetBudgetCommandHandler(repository).HandleAsync(new SetBudgetCommand(groupId, eventId, 25m, "CHF"), default);
        var addMember = new AddMemberCommandHandler(repository);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Carol"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Dan"), default);

        // Act: run the draw for the event, then generate reveal links for it.
        var drawResult = await new RunDrawCommandHandler(repository, generator, TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, eventId), default);
        var links = await new GetRevealLinksQueryHandler(repository, linkBuilder)
            .HandleAsync(new GetRevealLinksQuery(groupId, eventId), default);

        // Assert.
        Assert.True(drawResult.Success);
        Assert.Equal(4, drawResult.AssignmentCount);
        Assert.Equal(4, links.Count);

        var labels = links.Select(l => l.ForLabel).OrderBy(l => l).ToArray();
        Assert.Equal(_expectedLabels, labels);

        foreach (var link in links)
        {
            var (_, blob) = RevealLinkBuilder.ParseFragment(new Uri(link.Url).Fragment);
            var payload = codec.Decode(blob);
            Assert.Equal(25m, payload.BudgetAmount);
            Assert.Equal("CHF", payload.BudgetCurrency);
            Assert.NotEqual(link.ForLabel, payload.RecipientName);
        }
    }

    [Fact]
    public async Task GetRevealLinks_AfterMemberArchived_KeepsTheDrawAndItsLinks()
    {
        var repository = new InMemoryGroupRepository();
        var linkBuilder = new RevealLinkBuilder(new RevealLinkOptions("https://santa.example"), new RevealLinkCodec());

        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var eventId = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Carol"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Dan"), default);
        await new RunDrawCommandHandler(repository, new DrawGenerator(new SeededRandomSource(123)), TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, eventId), default);

        await new RemoveMemberCommandHandler(repository).HandleAsync(new RemoveMemberCommand(groupId, alice), default);

        // The draw is permanent history (ADR-0052): archiving a member keeps it, and its links still
        // generate because the archived member's name resolves through FindMember — so a past occasion
        // stays viewable and its already-issued links keep working. The UI flags it stale.
        var links = await new GetRevealLinksQueryHandler(repository, linkBuilder)
            .HandleAsync(new GetRevealLinksQuery(groupId, eventId), default);

        Assert.Equal(4, links.Count);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        var occasion = Assert.Single(group!.Events);
        Assert.True(occasion.HasCurrentDraw); // kept, only flagged stale
        Assert.True(occasion.DrawInvalidatedByMembershipChange);
        Assert.Equal(3, group.Members.Count); // Alice is archived, off the active roster
    }

    [Fact]
    public async Task GetRevealLinks_WhenDrawReferencesMissingMember_ReturnsNoLinksRatherThanPartialSet()
    {
        // Guards against a stored (pre-events) snapshot whose draw no longer matches its member set; the
        // legacy draw migrates into the default event (id == group id).
        var repository = new InMemoryGroupRepository();
        var linkBuilder = new RevealLinkBuilder(new RevealLinkOptions("https://santa.example"), new RevealLinkCodec());

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var ghost = Guid.NewGuid();
        var state = new GroupState(
            Guid.NewGuid(),
            "Family",
            Budget: null,
            Event: null,
            Members: [new MemberState(alice, "Alice"), new MemberState(bob, "Bob")],
            Exclusions: [],
            Draws:
            [
                new DrawState(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [new AssignmentState(alice, bob), new AssignmentState(bob, ghost)]),
            ]);

        await repository.SaveAsync(Group.FromState(state), default);

        var links = await new GetRevealLinksQueryHandler(repository, linkBuilder)
            .HandleAsync(new GetRevealLinksQuery(state.Id, state.Id), default);

        Assert.Empty(links);
    }

    [Fact]
    public async Task GetGroups_ReturnsSummaries()
    {
        var repository = new InMemoryGroupRepository();
        var create = new CreateGroupCommandHandler(repository);
        await create.HandleAsync(new CreateGroupCommand("One"), default);
        await create.HandleAsync(new CreateGroupCommand("Two"), default);

        var summaries = await new GetGroupsQueryHandler(repository).HandleAsync(new GetGroupsQuery(), default);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, s => s.Name == "One");
        Assert.Contains(summaries, s => s.Name == "Two");
        // A new group starts with no event (ADR-0042 amendment); the user adds the first occasion.
        Assert.All(summaries, s => Assert.Equal(0, s.EventCount));
    }
}
