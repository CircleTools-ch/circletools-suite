// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Application;
using CircleTools.Main.Service;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Covers the guard that closes FR-015's last open criterion: an account that still owns a circle
/// cannot be deleted, and the refusal names the circles so the UI can act on each one.
/// </summary>
/// <remarks>
/// The guard is a mapper over <c>GetSoleOwnedCirclesQuery</c>, so what is worth testing here is the
/// mapping — above all which of the two ways out each circle is given. Whether a particular person
/// counts as somebody who could receive ownership is <c>Circle.CountOtherAccountBackedPeople</c>'s
/// question, and is driven exhaustively in the Circles unit tests.
/// </remarks>
public sealed class SoleOwnerCircleGuardTests
{
    private static SoleOwnedCircleResponse Circle(string name, int otherAccountBackedPeople) =>
        new(Guid.CreateVersion7(), name, "Familie", otherAccountBackedPeople);

    [Fact]
    public async Task AnAccountThatOwnsNothing_MayBeDeleted()
    {
        var guard = new SoleOwnerCircleGuard(new StubSender([]));

        Assert.Null(await guard.CheckAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task ACircleNobodyElseCouldOwn_IsOfferedDeletion()
    {
        var guard = new SoleOwnerCircleGuard(new StubSender([Circle("Müller", otherAccountBackedPeople: 0)]));

        var refusal = await guard.CheckAsync(Guid.CreateVersion7());

        Assert.NotNull(refusal);
        Assert.Equal("circles_still_owned", refusal.Code);
        Assert.Equal("owner_must_delete_circle", Assert.Single(refusal.Blockers).Resolution);
    }

    /// <summary>
    /// The distinction the guard exists to make. A circle with other people in it is not "sole
    /// owned" in the narrow sense, and it would be easy to let it through — but deleting the account
    /// would strip the owner's membership and leave a circle nobody owns, which the domain never
    /// defines. It is still a blocker; only the way out differs.
    /// </summary>
    [Fact]
    public async Task ACircleSomebodyElseCouldOwn_IsStillABlocker_AndIsOfferedTransfer()
    {
        var guard = new SoleOwnerCircleGuard(new StubSender([Circle("Müller", otherAccountBackedPeople: 2)]));

        var refusal = await guard.CheckAsync(Guid.CreateVersion7());

        Assert.NotNull(refusal);
        Assert.Equal("owner_must_transfer_ownership", Assert.Single(refusal.Blockers).Resolution);
    }

    [Fact]
    public async Task EveryOwnedCircleIsNamedAndIdentified_SoTheUserIsNotSentHunting()
    {
        var circles = new[] { Circle("Müller", 0), Circle("Jassrunde", 3) };
        var guard = new SoleOwnerCircleGuard(new StubSender(circles));

        var refusal = await guard.CheckAsync(Guid.CreateVersion7());

        Assert.NotNull(refusal);
        Assert.Equal(
            circles.Select(c => (c.Id, c.Name)),
            refusal.Blockers.Select(b => (b.Id, b.Name)));

        // Mixed circles keep their own answer rather than one verdict for the batch.
        Assert.Equal(
            ["owner_must_delete_circle", "owner_must_transfer_ownership"],
            refusal.Blockers.Select(b => b.Resolution));
    }

    /// <summary>
    /// Answers the one query the guard is allowed to ask, and throws on anything else.
    /// </summary>
    private sealed class StubSender(IReadOnlyList<SoleOwnedCircleResponse> owned) : ISender
    {
        public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            Assert.IsType<GetSoleOwnedCirclesQuery>(query);

            return Task.FromResult((TResult)(object)owned);
        }

        public Task Send(ICommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
