using AppSuite.SecretSanta.Core.Application;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.Core.Test.TestSupport;

namespace AppSuite.SecretSanta.Core.Test;

/// <summary>Tests exercising the application handlers over an in-memory repository.</summary>
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

        // Arrange a group with four members and a budget.
        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        await new SetBudgetCommandHandler(repository).HandleAsync(new SetBudgetCommand(groupId, 25m, "CHF"), default);
        var addMember = new AddMemberCommandHandler(repository);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Alice", ["Scarf"]), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Bob", []), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Carol", ["Tea"]), default);
        await addMember.HandleAsync(new AddMemberCommand(groupId, "Dan", []), default);

        // Act: run the draw, then generate reveal links.
        var drawResult = await new RunDrawCommandHandler(repository, generator, TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId), default);
        var links = await new GetRevealLinksQueryHandler(repository, linkBuilder)
            .HandleAsync(new GetRevealLinksQuery(groupId), default);

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
    }
}
