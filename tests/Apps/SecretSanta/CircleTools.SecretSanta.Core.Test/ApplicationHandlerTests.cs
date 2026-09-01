using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Per-handler tests for the Secret-Santa application layer, exercising each command/query over the
/// in-memory repository fake (happy path + a representative validation/error path). ADR-0042: budget,
/// event details and wishlists are per event. A new group starts with <strong>no</strong> event
/// (ADR-0042 amendment), so event-scoped tests add one first.
/// </summary>
public sealed class ApplicationHandlerTests
{
    private static async Task<(InMemoryGroupRepository Repository, Guid GroupId)> CreateGroupAsync(string name = "Family")
    {
        var repository = new InMemoryGroupRepository();
        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand(name), default);
        return (repository, groupId);
    }

    [Fact]
    public async Task CreateGroup_TrimsNameAndPersists()
    {
        var repository = new InMemoryGroupRepository();

        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("  Office  "), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.NotNull(group);
        Assert.Equal("Office", group!.Name);
    }

    [Fact]
    public async Task RenameGroup_ChangesName()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await new RenameGroupCommandHandler(repository).HandleAsync(new RenameGroupCommand(groupId, "Renamed"), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Equal("Renamed", group!.Name);
    }

    [Fact]
    public async Task RenameGroup_UnknownGroup_Throws()
    {
        var repository = new InMemoryGroupRepository();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RenameGroupCommandHandler(repository).HandleAsync(new RenameGroupCommand(Guid.NewGuid(), "Nope"), default));
    }

    [Fact]
    public async Task SetBudget_SetsThenClears()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var eventId = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var handler = new SetBudgetCommandHandler(repository);
        var getEvent = new GetEventQueryHandler(repository, TimeProvider.System);

        await handler.HandleAsync(new SetBudgetCommand(groupId, eventId, 30m, "CHF"), default);
        var withBudget = await getEvent.HandleAsync(new GetEventQuery(groupId, eventId), default);
        Assert.NotNull(withBudget!.Budget);
        Assert.Equal(30m, withBudget.Budget!.Amount);
        Assert.Equal("CHF", withBudget.Budget.Currency);

        await handler.HandleAsync(new SetBudgetCommand(groupId, eventId, null, null), default);
        var cleared = await getEvent.HandleAsync(new GetEventQuery(groupId, eventId), default);
        Assert.Null(cleared!.Budget);
    }

    [Fact]
    public async Task SetEventDetails_SetsThenClears()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var eventId = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var handler = new SetEventDetailsCommandHandler(repository);
        var getEvent = new GetEventQueryHandler(repository, TimeProvider.System);
        var date = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, eventId, date, "Zurich", "Bring snacks"), default);
        var withEvent = await getEvent.HandleAsync(new GetEventQuery(groupId, eventId), default);
        Assert.NotNull(withEvent!.Details);
        Assert.Equal("Zurich", withEvent.Details!.Location);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, eventId, null, null, null), default);
        var cleared = await getEvent.HandleAsync(new GetEventQuery(groupId, eventId), default);
        Assert.Null(cleared!.Details);
    }

    [Fact]
    public async Task AddMember_ReturnsIdAndPersists()
    {
        var (repository, groupId) = await CreateGroupAsync();

        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Alice"), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        var member = Assert.Single(group!.Members);
        Assert.Equal(memberId, member.Id);
        Assert.Equal("Alice", member.Name);
    }

    [Fact]
    public async Task SetMemberWishlist_PersistsPerEvent()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        var addEvent = new AddEventCommandHandler(repository);
        var first = await addEvent.HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var second = await addEvent.HandleAsync(new AddEventCommand(groupId, "Birthday"), default);

        await new SetMemberWishlistCommandHandler(repository)
            .HandleAsync(new SetMemberWishlistCommand(groupId, first, memberId, ["Scarf", "Book"]), default);
        await new SetMemberWishlistCommandHandler(repository)
            .HandleAsync(new SetMemberWishlistCommand(groupId, second, memberId, ["Bike"]), default);

        var getEvent = new GetEventQueryHandler(repository, TimeProvider.System);
        var christmas = await getEvent.HandleAsync(new GetEventQuery(groupId, first), default);
        var birthday = await getEvent.HandleAsync(new GetEventQuery(groupId, second), default);
        Assert.Equal(["Scarf", "Book"], Assert.Single(christmas!.Wishlists).Items);
        Assert.Equal(["Bike"], Assert.Single(birthday!.Wishlists).Items);
    }

    [Fact]
    public async Task AddMember_DuplicateName_Throws()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var handler = new AddMemberCommandHandler(repository);
        await handler.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);

        await Assert.ThrowsAsync<DuplicateMemberNameException>(() =>
            handler.HandleAsync(new AddMemberCommand(groupId, "alice"), default));
    }

    [Fact]
    public async Task EditMember_UpdatesName()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Bob"), default);

        await new EditMemberCommandHandler(repository)
            .HandleAsync(new EditMemberCommand(groupId, memberId, "Robert"), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        var member = Assert.Single(group!.Members);
        Assert.Equal("Robert", member.Name);
    }

    [Fact]
    public async Task EditMember_UnknownMember_Throws()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EditMemberCommandHandler(repository)
                .HandleAsync(new EditMemberCommand(groupId, Guid.NewGuid(), "Ghost"), default));
    }

    [Fact]
    public async Task RemoveMember_RemovesMemberAndReferencingExclusions()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await new AddExclusionCommandHandler(repository).HandleAsync(new AddExclusionCommand(groupId, alice, bob), default);

        await new RemoveMemberCommandHandler(repository).HandleAsync(new RemoveMemberCommand(groupId, bob), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Single(group!.Members);
        Assert.Empty(group.Exclusions);
    }

    [Fact]
    public async Task AddExclusion_AddsDirectedPair()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);

        await new AddExclusionCommandHandler(repository).HandleAsync(new AddExclusionCommand(groupId, alice, bob), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        var exclusion = Assert.Single(group!.Exclusions);
        Assert.Equal(alice, exclusion.GiverMemberId);
        Assert.Equal(bob, exclusion.RecipientMemberId);
    }

    [Fact]
    public async Task AddExclusion_NonMemberEndpoint_Throws()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var alice = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Alice"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddExclusionCommandHandler(repository)
                .HandleAsync(new AddExclusionCommand(groupId, alice, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task RemoveExclusion_RemovesPair()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice"), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob"), default);
        await new AddExclusionCommandHandler(repository).HandleAsync(new AddExclusionCommand(groupId, alice, bob), default);

        await new RemoveExclusionCommandHandler(repository)
            .HandleAsync(new RemoveExclusionCommand(groupId, alice, bob), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Empty(group!.Exclusions);
    }

    [Fact]
    public async Task NewGroup_HasNoEvents_AndAddRemoveGoesToZero()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var getGroup = new GetGroupQueryHandler(repository, TimeProvider.System);

        // A new group starts with no event (ADR-0042 amendment): the user creates the first one.
        var group = await getGroup.HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Empty(group!.Events);

        var addEvent = new AddEventCommandHandler(repository);
        var first = await addEvent.HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var second = await addEvent.HandleAsync(new AddEventCommand(groupId, "Birthday"), default);
        group = await getGroup.HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Equal(2, group!.Events.Count);

        // Removing the last event is now allowed — a group may hold zero occasions.
        var removeEvent = new RemoveEventCommandHandler(repository);
        await removeEvent.HandleAsync(new RemoveEventCommand(groupId, second), default);
        await removeEvent.HandleAsync(new RemoveEventCommand(groupId, first), default);
        group = await getGroup.HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Empty(group!.Events);
    }

    [Fact]
    public async Task DeleteGroup_RemovesGroup()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await new DeleteGroupCommandHandler(repository).HandleAsync(new DeleteGroupCommand(groupId), default);

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Null(group);
    }

    [Fact]
    public async Task GetGroup_UnknownGroup_ReturnsNull()
    {
        var repository = new InMemoryGroupRepository();

        var group = await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(Guid.NewGuid()), default);

        Assert.Null(group);
    }
}
