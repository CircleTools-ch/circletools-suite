// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// The exclusions page warns as soon as exclusions rule out a draw (Q13a), and a drawn event says when its
/// draw no longer matches the exclusions (Q13b); the event card counts filled wishlists (I5).
/// </summary>
public sealed class DrawFeasibilityTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid D = Guid.NewGuid();

    [Fact]
    public void NoExclusions_IsPossible()
    {
        var result = DrawFeasibility.Check([A, B, C, D], []);

        Assert.True(result.Possible);
        Assert.Empty(result.WithoutRecipient);
        Assert.Empty(result.WithoutGiver);
    }

    [Fact]
    public void SomeoneExcludedFromEveryone_HasNobodyToGiveTo()
    {
        var result = DrawFeasibility.Check(
            [A, B, C],
            [Exclusion.Create(A, B), Exclusion.Create(A, C)]);

        Assert.False(result.Possible);
        Assert.Equal([A], result.WithoutRecipient);
    }

    [Fact]
    public void SomeoneEveryoneIsExcludedFrom_HasNobodyGivingToThem()
    {
        var result = DrawFeasibility.Check(
            [A, B, C],
            [Exclusion.Create(B, A), Exclusion.Create(C, A)]);

        Assert.False(result.Possible);
        Assert.Equal([A], result.WithoutGiver);
    }

    [Fact]
    public void EveryoneHasOptions_ButTheNoReciprocalRuleLeavesNoDraw_IsImpossible()
    {
        // Three people, A ↔ B excluded both ways: A and B must both give to C. Nobody is stuck on their own.
        var result = DrawFeasibility.Check([A, B, C], [Exclusion.Create(A, B, mutual: true)]);

        Assert.Empty(result.WithoutRecipient);
        Assert.Empty(result.WithoutGiver);
        Assert.False(result.Possible);
    }

    [Fact]
    public void TightButSolvable_IsPossible()
    {
        // Four people, two mutual couples: A→C, C→B, B→D, D→A works.
        var result = DrawFeasibility.Check(
            [A, B, C, D],
            [Exclusion.Create(A, B, mutual: true), Exclusion.Create(C, D, mutual: true)]);

        Assert.True(result.Possible);
    }

    [Fact]
    public void FewerThanThree_IsNotJudged()
    {
        Assert.Null(DrawFeasibility.Check([A, B], []).Possible);
    }

    [Fact]
    public async Task TheQuery_NamesWhoIsStuck_AndTheSummaryFlagsADrawTheExclusionsNowBreak()
    {
        var repository = new InMemoryGroupRepository();
        var groupId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family"), default);
        var eventId = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(groupId, "Christmas"), default);
        var add = new AddMemberCommandHandler(repository);
        var ann = await add.HandleAsync(new AddMemberCommand(groupId, "Ann"), default);
        var ben = await add.HandleAsync(new AddMemberCommand(groupId, "Ben"), default);
        var cat = await add.HandleAsync(new AddMemberCommand(groupId, "Cat"), default);
        var dan = await add.HandleAsync(new AddMemberCommand(groupId, "Dan"), default);
        await new SetMemberWishlistCommandHandler(repository).HandleAsync(new SetMemberWishlistCommand(groupId, eventId, ann, ["Book"]), default);
        await new RunDrawCommandHandler(repository, new DrawGenerator(new SeededRandomSource(7)), TimeProvider.System)
            .HandleAsync(new RunDrawCommand(groupId, eventId), default);

        var before = (await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default))!;
        Assert.False(before.Events[0].DrawBreaksExclusions);
        Assert.Equal(1, before.Events[0].WishlistsFilled);

        // Exclude whoever Ann drew, then everyone else for Ann: the draw breaks, and Ann is stuck.
        var group = (await repository.GetAsync(groupId, default))!;
        var annDrew = group.Events[0].CurrentDraw!.Assignments.Single(a => a.GiverMemberId == ann).RecipientMemberId;
        var addExclusion = new AddExclusionCommandHandler(repository);
        foreach (var other in new[] { ben, cat, dan })
        {
            await addExclusion.HandleAsync(new AddExclusionCommand(groupId, ann, other), default);
        }

        var after = (await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(groupId), default))!;
        Assert.True(after.Events[0].DrawBreaksExclusions);
        Assert.NotEqual(Guid.Empty, annDrew);

        var feasibility = (await new CheckDrawFeasibilityQueryHandler(repository).HandleAsync(new CheckDrawFeasibilityQuery(groupId), default))!;
        Assert.False(feasibility.Possible);
        Assert.Equal(["Ann"], feasibility.WithoutRecipient);
    }
}
