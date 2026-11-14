// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Test.TestSupport;
using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Tests for the <see cref="DrawGenerator"/> domain service.</summary>
public sealed class DrawGeneratorTests
{
    private static List<Guid> Members(int count) =>
        Enumerable.Range(0, count).Select(_ => GuidV7.New()).ToList();

    [Fact]
    public void Generate_ProducesValidDerangement()
    {
        var members = Members(5);
        var generator = new DrawGenerator(new SeededRandomSource(1));

        var result = generator.Generate(members, [], previous: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(members.Count, result.Assignments.Count);

        // Every member gives exactly once and receives exactly once, and nobody draws themselves.
        Assert.Equal(members.OrderBy(m => m), result.Assignments.Select(a => a.GiverMemberId).OrderBy(m => m));
        Assert.Equal(members.OrderBy(m => m), result.Assignments.Select(a => a.RecipientMemberId).OrderBy(m => m));
        Assert.DoesNotContain(result.Assignments, a => a.GiverMemberId == a.RecipientMemberId);
    }

    [Fact]
    public void Generate_HonorsExclusions()
    {
        var members = Members(5);
        var exclusions = new List<Exclusion>
        {
            Exclusion.Create(members[0], members[1]),
            Exclusion.Create(members[2], members[3]),
        };
        var generator = new DrawGenerator(new SeededRandomSource(7));

        var result = generator.Generate(members, exclusions, previous: null);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Assignments, a => a.GiverMemberId == members[0] && a.RecipientMemberId == members[1]);
        Assert.DoesNotContain(result.Assignments, a => a.GiverMemberId == members[2] && a.RecipientMemberId == members[3]);
    }

    [Fact]
    public void Generate_MutualExclusion_ForbidsBothDirections()
    {
        var members = Members(5);
        var exclusions = new List<Exclusion>
        {
            Exclusion.Create(members[0], members[1], mutual: true),
        };
        var generator = new DrawGenerator(new SeededRandomSource(7));

        var result = generator.Generate(members, exclusions, previous: null);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Assignments, a => a.GiverMemberId == members[0] && a.RecipientMemberId == members[1]);
        Assert.DoesNotContain(result.Assignments, a => a.GiverMemberId == members[1] && a.RecipientMemberId == members[0]);
    }

    [Fact]
    public void Generate_HasNoReciprocalPairs()
    {
        var members = Members(6);
        var generator = new DrawGenerator(new SeededRandomSource(42));

        var result = generator.Generate(members, [], previous: null);

        Assert.True(result.IsSuccess);
        var map = result.Assignments.ToDictionary(a => a.GiverMemberId, a => a.RecipientMemberId);
        foreach (var (giver, recipient) in map)
        {
            Assert.NotEqual(giver, map[recipient]);
        }
    }

    [Fact]
    public void Generate_DiffersFromPreviousDraw()
    {
        var members = Members(4);
        var generator = new DrawGenerator(new SeededRandomSource(3));

        var first = generator.Generate(members, [], previous: null);
        Assert.True(first.IsSuccess);
        var previous = first.Assignments.ToDictionary(a => a.GiverMemberId, a => a.RecipientMemberId);

        var second = generator.Generate(members, [], previous);

        Assert.True(second.IsSuccess);
        var identical = second.Assignments.All(a => previous[a.GiverMemberId] == a.RecipientMemberId);
        Assert.False(identical);
    }

    [Fact]
    public void Generate_IsDeterministicForFixedSeed()
    {
        var members = Members(6);

        var a = new DrawGenerator(new SeededRandomSource(99)).Generate(members, [], previous: null);
        var b = new DrawGenerator(new SeededRandomSource(99)).Generate(members, [], previous: null);

        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        Assert.Equal(
            a.Assignments.Select(x => (x.GiverMemberId, x.RecipientMemberId)),
            b.Assignments.Select(x => (x.GiverMemberId, x.RecipientMemberId)));
    }

    [Fact]
    public void Generate_TwoMembers_FailsAsReciprocalImpossible()
    {
        var members = Members(2);
        var generator = new DrawGenerator(new SeededRandomSource(1));

        var result = generator.Generate(members, [], previous: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(DrawFailureReason.TwoMemberReciprocalImpossible, result.FailureReason);
    }

    [Fact]
    public void Generate_OverConstrained_FailsAsOverConstrained()
    {
        // Three members where one giver is excluded from both possible recipients leaves no derangement.
        var members = Members(3);
        var exclusions = new List<Exclusion>
        {
            Exclusion.Create(members[0], members[1]),
            Exclusion.Create(members[0], members[2]),
        };
        var generator = new DrawGenerator(new SeededRandomSource(5));

        var result = generator.Generate(members, exclusions, previous: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(DrawFailureReason.OverConstrained, result.FailureReason);
    }
}
