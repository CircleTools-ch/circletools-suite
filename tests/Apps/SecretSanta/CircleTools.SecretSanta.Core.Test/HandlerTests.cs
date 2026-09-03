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
        var eventId = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
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
    public async Task GetRevealLinks_CarriesEachRecipientsGiftNote_ToTheirGiversLink()
    {
        // FR-037: the giver sees their *recipient's* gift note on the reveal. Give exactly one member a
        // note, then assert exactly one link decodes to it — and it is not that member's own link.
        var repository = new InMemoryGroupRepository();
        var codec = new RevealLinkCodec();
        var linkBuilder = new RevealLinkBuilder(new RevealLinkOptions("https://santa.example"), codec);

        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var eventId = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Carol"), default);
        await new SetMemberGiftNoteCommandHandler(repository)
            .HandleAsync(new SetMemberGiftNoteCommand(groupId, alice, "No nuts — allergy"), default);
        await new RunDrawCommandHandler(repository, new DrawGenerator(new SeededRandomSource(7)), TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, eventId), default);

        var links = await new GetRevealLinksQueryHandler(repository, linkBuilder)
            .HandleAsync(new GetRevealLinksQuery(groupId, eventId), default);

        var withNote = links
            .Select(l => codec.Decode(RevealLinkBuilder.ParseFragment(new Uri(l.Url).Fragment).Item2))
            .Where(p => p.GiftNote is not null)
            .ToList();

        var only = Assert.Single(withNote);
        Assert.Equal("No nuts — allergy", only.GiftNote);
        Assert.Equal("Alice", only.RecipientName); // the note is the *recipient's*, shown to whoever drew Alice
    }

    [Fact]
    public async Task AddEvent_CarryingForward_CopiesBudgetAndWishlist_IsUndrawn_AndCarriesBaseline()
    {
        var repository = new InMemoryGroupRepository();

        // A first, drawn occasion with a budget and one member's wishlist.
        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var source = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(groupId, "Christmas 2026"), default);
        await new SetBudgetCommandHandler(repository).HandleAsync(new SetBudgetCommand(groupId, source, 30m, "CHF"), default);
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Carol"), default);
        await new SetMemberWishlistCommandHandler(repository).HandleAsync(new SetMemberWishlistCommand(groupId, source, alice, ["Scarf"]), default);
        await new RunDrawCommandHandler(repository, new DrawGenerator(new SeededRandomSource(11)), TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, source), default);

        // Carry a new occasion forward from it.
        var next = await new AddEventCommandHandler(repository, TimeProvider.System)
            .HandleAsync(new AddEventCommand(groupId, "Christmas 2027", source), default);

        var getEvent = new GetEventQueryHandler(repository, TimeProvider.System);
        var nextDto = await getEvent.HandleAsync(new GetEventQuery(groupId, next), default);

        Assert.NotNull(nextDto);
        Assert.Equal(30m, nextDto!.Budget!.Amount);                                   // budget copied
        Assert.Equal(["Scarf"], Assert.Single(nextDto.Wishlists, w => w.MemberId == alice).Items); // wishlist copied
        Assert.False(nextDto.HasCurrentDraw);                                          // undrawn — no reveal links
        Assert.True(nextDto.HasCarriedForwardBaseline);                               // baseline present for rule (c)

        // The new draw avoids the source's pairings.
        var sourceDto = await getEvent.HandleAsync(new GetEventQuery(groupId, source), default);
        await new RunDrawCommandHandler(repository, new DrawGenerator(new SeededRandomSource(11)), TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, next), default);
        var drawnNext = await getEvent.HandleAsync(new GetEventQuery(groupId, next), default);

        var sourcePairs = sourceDto!.CurrentAssignments.Select(a => (a.GiverMemberId, a.RecipientMemberId)).OrderBy(p => p.GiverMemberId).ToList();
        var nextPairs = drawnNext!.CurrentAssignments.Select(a => (a.GiverMemberId, a.RecipientMemberId)).OrderBy(p => p.GiverMemberId).ToList();
        Assert.NotEqual(sourcePairs, nextPairs);
        Assert.False(drawnNext.HasCarriedForwardBaseline);                           // cleared once a real draw ran
    }

    [Fact]
    public async Task GetRevealLinks_AfterMemberArchived_KeepsTheDrawAndItsLinks()
    {
        var repository = new InMemoryGroupRepository();
        var linkBuilder = new RevealLinkBuilder(new RevealLinkOptions("https://santa.example"), new RevealLinkCodec());

        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var eventId = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
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

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
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

        var summaries = await new GetGroupsQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupsQuery(), default);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, s => s.Name == "One");
        Assert.Contains(summaries, s => s.Name == "Two");
        // A new group starts with no event (ADR-0042 amendment); the user adds the first occasion.
        Assert.All(summaries, s => Assert.Equal(0, s.EventCount));
    }

    [Fact]
    public async Task StaleDraw_IsFlaggedOnAnUpcomingEvent_ButNotOnAPastOne()
    {
        // A past occasion's stale draw is history, not something to re-draw, so the read model suppresses
        // the "re-draw needed" flag once the date is in the past — only the upcoming one keeps it (ADR-0052).
        var repo = new InMemoryGroupRepository();
        var groupId = await new CreateGroupCommandHandler(repo).HandleAsync(new CreateGroupCommand("Fam"), default);
        var addEvent = new AddEventCommandHandler(repo, TimeProvider.System);
        var past = await addEvent.HandleAsync(new AddEventCommand(groupId, "Christmas 2024"), default);
        var upcoming = await addEvent.HandleAsync(new AddEventCommand(groupId, "Christmas 2027"), default);
        var setDetails = new SetEventDetailsCommandHandler(repo);
        await setDetails.HandleAsync(new SetEventDetailsCommand(groupId, past, new DateTimeOffset(2024, 12, 24, 0, 0, 0, TimeSpan.Zero), "", ""), default);
        await setDetails.HandleAsync(new SetEventDetailsCommand(groupId, upcoming, new DateTimeOffset(2027, 12, 24, 0, 0, 0, TimeSpan.Zero), "", ""), default);
        var addMember = new AddMemberCommandHandler(repo);
        var a = await addMember.HandleAsync(new AddMemberCommand(groupId, "A"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "B"), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "C"), default);
        var draw = new RunDrawCommandHandler(repo, new DrawGenerator(new SeededRandomSource(1)), TimeProvider.System);
        await draw.HandleAsync(new RunDrawCommand(groupId, past), default);
        await draw.HandleAsync(new RunDrawCommand(groupId, upcoming), default);

        // A roster change flags both draws stale in the domain.
        await new RemoveMemberCommandHandler(repo).HandleAsync(new RemoveMemberCommand(groupId, a), default);

        var clock = new FixedClock(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var group = await new GetGroupQueryHandler(repo, clock).HandleAsync(new GetGroupQuery(groupId), default);

        Assert.False(group!.Events.Single(e => e.Id == past).DrawInvalidatedByMembershipChange);
        Assert.True(group.Events.Single(e => e.Id == upcoming).DrawInvalidatedByMembershipChange);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
