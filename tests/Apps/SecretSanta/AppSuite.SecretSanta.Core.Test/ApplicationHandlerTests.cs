using AppSuite.SecretSanta.Core.Application;
using AppSuite.SecretSanta.Core.Test.TestSupport;

namespace AppSuite.SecretSanta.Core.Test;

/// <summary>
/// Per-handler tests for the Secret-Santa application layer, exercising each command/query over the
/// in-memory repository fake (happy path + a representative validation/error path).
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

        await handler.HandleAsync(new SetBudgetCommand(groupId, 30m, "CHF"), default);
        var withBudget = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.NotNull(withBudget!.Budget);
        Assert.Equal(30m, withBudget.Budget!.Amount);
        Assert.Equal("CHF", withBudget.Budget.Currency);

        await handler.HandleAsync(new SetBudgetCommand(groupId, null, null), default);
        var cleared = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Null(cleared!.Budget);
    }

    [Fact]
    public async Task SetEventDetails_SetsThenClears()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var handler = new SetEventDetailsCommandHandler(repository);
        var date = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, date, "Zurich", "Bring snacks"), default);
        var withEvent = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.NotNull(withEvent!.Event);
        Assert.Equal("Zurich", withEvent.Event!.Location);

        await handler.HandleAsync(new SetEventDetailsCommand(groupId, null, null, null), default);
        var cleared = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Null(cleared!.Event);
    }

    [Fact]
    public async Task AddMember_ReturnsIdAndPersistsWishlist()
    {
        var (repository, groupId) = await CreateGroupAsync();

        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Alice", ["Scarf", "Book"]), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        var member = Assert.Single(group!.Members);
        Assert.Equal(memberId, member.Id);
        Assert.Equal("Alice", member.Name);
        Assert.Equal(["Scarf", "Book"], member.Wishlist);
    }

    [Fact]
    public async Task AddMember_DuplicateName_Throws()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var handler = new AddMemberCommandHandler(repository);
        await handler.HandleAsync(new AddMemberCommand(groupId, "Alice", []), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new AddMemberCommand(groupId, "alice", []), default));
    }

    [Fact]
    public async Task EditMember_UpdatesNameAndWishlist()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var memberId = await new AddMemberCommandHandler(repository)
            .HandleAsync(new AddMemberCommand(groupId, "Bob", ["Socks"]), default);

        await new EditMemberCommandHandler(repository)
            .HandleAsync(new EditMemberCommand(groupId, memberId, "Robert", ["Gloves"]), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        var member = Assert.Single(group!.Members);
        Assert.Equal("Robert", member.Name);
        Assert.Equal(["Gloves"], member.Wishlist);
    }

    [Fact]
    public async Task EditMember_UnknownMember_Throws()
    {
        var (repository, groupId) = await CreateGroupAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EditMemberCommandHandler(repository)
                .HandleAsync(new EditMemberCommand(groupId, Guid.NewGuid(), "Ghost", []), default));
    }

    [Fact]
    public async Task RemoveMember_RemovesMemberAndReferencingExclusions()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice", []), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob", []), default);
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
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice", []), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob", []), default);

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
            .HandleAsync(new AddMemberCommand(groupId, "Alice", []), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddExclusionCommandHandler(repository)
                .HandleAsync(new AddExclusionCommand(groupId, alice, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task RemoveExclusion_RemovesPair()
    {
        var (repository, groupId) = await CreateGroupAsync();
        var addMember = new AddMemberCommandHandler(repository);
        var alice = await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice", []), default);
        var bob = await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob", []), default);
        await new AddExclusionCommandHandler(repository).HandleAsync(new AddExclusionCommand(groupId, alice, bob), default);

        await new RemoveExclusionCommandHandler(repository)
            .HandleAsync(new RemoveExclusionCommand(groupId, alice, bob), default);

        var group = await new GetGroupQueryHandler(repository).HandleAsync(new GetGroupQuery(groupId), default);
        Assert.Empty(group!.Exclusions);
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
