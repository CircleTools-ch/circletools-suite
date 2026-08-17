using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Per-handler tests for the Secret-Santa application layer, exercising each command/query over the
/// in-memory repository fake (happy path + a representative validation/error path). ADR-0042: budget,
/// event details and wishlists are per event; a new group's default event has id == group id.
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

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.NotNull(group);
        Assert.Equal("Office", group!.Name);
    }

    [Fact]
    public async Task RenameGroup_ChangesName()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await new RenameGroupCommandHandler(repository).HandleAsync(new RenameGroupCommand(groupId, "Renamed"), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
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
        var handler = new SetBudgetCommandHandler(repository);
        var getEvent = new GetEventQueryHandler(repository);

        await handler.HandleAsync(new SetBudgetCommand(groupId, groupId, 30m, "CHF"), default);
        var withBudget = await getEvent.HandleAsync(new GetEventQuery(groupId, groupId), default);
        Assert.NotNull(withBudget!.Budget);
        Assert.Equal(30m, withBudget.Budget!.Amount);
        Assert.Equal("CHF", withBudget.Budget.Currency);

        await handler.HandleAsync(new SetBudgetCommand(groupId, groupId, null, null), default);
        var cleared = await getEvent.HandleAsync(new GetEventQuery(groupId, groupId), default);
        Assert.Null(cleared!.Budget);
    }

    [Fact]
    public async Task SetEventDetails_SetsThenClears()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var handler = new SetEventDetailsCommandHandler(repository);
        var getEvent = new GetEventQueryHandler(repository);
        var date = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, groupId, date, "Zurich", "Bring snacks"), default);
        var withEvent = await getEvent.HandleAsync(new GetEventQuery(groupId, groupId), default);
        Assert.NotNull(withEvent!.Details);
        Assert.Equal("Zurich", withEvent.Details!.Location);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, groupId, null, null, null), default);
        var cleared = await getEvent.HandleAsync(new GetEventQuery(groupId, groupId), default);
        Assert.Null(cleared!.Details);
    }

    [Fact]
    public async Task AddMember_ReturnsIdAndPersists()
    {
        var (repository, groupId) = await CreateGroupAsync();

        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Alice"), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
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
        var second = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Birthday"), default);

        await new SetMemberWishlistCommandHandler(repository)
            .HandleAsync(new SetMemberWishlistCommand(groupId, groupId, memberId, ["Scarf", "Book"]), default);
        await new SetMemberWishlistCommandHandler(repository)
            .HandleAsync(new SetMemberWishlistCommand(groupId, second, memberId, ["Bike"]), default);

        var getEvent = new GetEventQueryHandler(repository);
        var defaultEvent = await getEvent.HandleAsync(new GetEventQuery(groupId, groupId), default);
        var birthday = await getEvent.HandleAsync(new GetEventQuery(groupId, second), default);
        Assert.Equal(["Scarf", "Book"], Assert.Single(defaultEvent!.Wishlists).Items);
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

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
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

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
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

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
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

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Empty(group!.Exclusions);
    }

    [Fact]
    public async Task AddEvent_ThenRemove_KeepsAtLeastOne()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var second = await new AddEventCommandHandler(repository).HandleAsync(new AddEventCommand(groupId, "Birthday"), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Equal(2, group!.Events.Count);

        await new RemoveEventCommandHandler(repository).HandleAsync(new RemoveEventCommand(groupId, second), default);
        group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Single(group!.Events);

        // The last remaining event cannot be removed.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveEventCommandHandler(repository).HandleAsync(new RemoveEventCommand(groupId, groupId), default));
    }

    [Fact]
    public async Task DeleteGroup_RemovesGroup()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await new DeleteGroupCommandHandler(repository).HandleAsync(new DeleteGroupCommand(groupId), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Null(group);
    }

    [Fact]
    public async Task GetGroup_UnknownGroup_ReturnsNull()
    {
        var repository = new InMemoryGroupRepository();

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(Guid.NewGuid()), default);

        Assert.Null(group);
    }
}
