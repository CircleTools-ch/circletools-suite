using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository.Test.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Circles.Repository.Test;

/// <summary>
/// FR-017's three application-level invitation limits, against real Postgres.
/// </summary>
/// <remarks>
/// The windows are rolling, so the clock is the interesting input and it is injected. Each test uses
/// fresh identifiers and a fresh address hash, because the limits are keyed on exactly those and the
/// container is shared.
/// </remarks>
[Collection(SharedCirclesDatabase.CollectionName)]
public sealed class InvitationRateLimiterTests(CirclesPostgresFixture fixture)
{
    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task AnInviter_IsStoppedAfterTwentyInvitationsInAnHour_AndToldWhenTheyMayContinue()
    {
        var inviter = Guid.CreateVersion7();
        var limits = new CircleLimits();

        await using var provider = fixture.CreateProvider();
        var clock = new MovableClock(CircleFactory.Now);
        var limiter = Limiter(provider, clock);

        for (var i = 0; i < limits.InvitationsPerInviterPerHour; i++)
        {
            // A different address each time, so only the per-inviter limit is in play.
            var allowed = await limiter.TryConsumeAsync(inviter, Address(), limits);
            Assert.True(allowed.IsAllowed);

            clock.Now = clock.Now.AddMinutes(1);
        }

        var refused = await limiter.TryConsumeAsync(inviter, Address(), limits);

        Assert.False(refused.IsAllowed);

        // The refusal says when, not only that. The window frees when its oldest attempt falls out.
        Assert.Equal(CircleFactory.Now.AddHours(1), refused.RetryAfter);
    }

    [Fact]
    public async Task TheHourlyWindow_Rolls_RatherThanResettingOnTheHour()
    {
        // A fixed window that resets on the hour lets twice the limit through across the boundary,
        // which for anything trying to use the suite as a mail relay is the only boundary that
        // matters.
        var inviter = Guid.CreateVersion7();
        var limits = new CircleLimits();

        await using var provider = fixture.CreateProvider();
        var clock = new MovableClock(CircleFactory.Now);
        var limiter = Limiter(provider, clock);

        for (var i = 0; i < limits.InvitationsPerInviterPerHour; i++)
        {
            Assert.True((await limiter.TryConsumeAsync(inviter, Address(), limits)).IsAllowed);
        }

        Assert.False((await limiter.TryConsumeAsync(inviter, Address(), limits)).IsAllowed);

        // A minute after the first attempt fell out of the window, and no sooner.
        clock.Now = CircleFactory.Now.AddHours(1).AddMinutes(1);

        Assert.True((await limiter.TryConsumeAsync(inviter, Address(), limits)).IsAllowed);
    }

    [Fact]
    public async Task AnInviter_IsStoppedAfterSixtyInvitationsInADay()
    {
        var inviter = Guid.CreateVersion7();
        var limits = new CircleLimits();

        await using var provider = fixture.CreateProvider();
        var clock = new MovableClock(CircleFactory.Now);
        var limiter = Limiter(provider, clock);

        // Spread across the day so the hourly limit is never the one refusing.
        for (var i = 0; i < limits.InvitationsPerInviterPerDay; i++)
        {
            Assert.True((await limiter.TryConsumeAsync(inviter, Address(), limits)).IsAllowed);
            clock.Now = clock.Now.AddMinutes(20);
        }

        var refused = await limiter.TryConsumeAsync(inviter, Address(), limits);

        Assert.False(refused.IsAllowed);
        Assert.Equal(CircleFactory.Now.AddDays(1), refused.RetryAfter);
    }

    [Fact]
    public async Task AnAddress_ReceivesNoMoreThanThreeInvitationsADay_WhoeverSendsThem()
    {
        // The only limit that protects the third party. Keyed on the inviter it would be bypassed
        // by inviting from three circles — or, as here, by three different people.
        var recipient = Address();
        var limits = new CircleLimits();

        await using var provider = fixture.CreateProvider();
        var clock = new MovableClock(CircleFactory.Now);
        var limiter = Limiter(provider, clock);

        for (var i = 0; i < limits.InvitationsPerRecipientPerDay; i++)
        {
            var allowed = await limiter.TryConsumeAsync(Guid.CreateVersion7(), recipient, limits);
            Assert.True(allowed.IsAllowed);

            clock.Now = clock.Now.AddHours(2);
        }

        var refused = await limiter.TryConsumeAsync(Guid.CreateVersion7(), recipient, limits);

        Assert.False(refused.IsAllowed);
        Assert.Equal(CircleFactory.Now.AddDays(1), refused.RetryAfter);
    }

    [Fact]
    public async Task ARefusedAttempt_IsNotCounted()
    {
        // Charging refused attempts would keep the window permanently full for anyone retrying, so
        // a limit hit once would never lift.
        var recipient = Address();
        var limits = new CircleLimits { InvitationsPerRecipientPerDay = 1 };

        await using var provider = fixture.CreateProvider();
        var clock = new MovableClock(CircleFactory.Now);
        var limiter = Limiter(provider, clock);

        Assert.True((await limiter.TryConsumeAsync(Guid.CreateVersion7(), recipient, limits)).IsAllowed);

        clock.Now = clock.Now.AddHours(12);
        Assert.False((await limiter.TryConsumeAsync(Guid.CreateVersion7(), recipient, limits)).IsAllowed);

        // A day after the *first* attempt — not after the refused one.
        clock.Now = CircleFactory.Now.AddDays(1).AddMinutes(1);
        Assert.True((await limiter.TryConsumeAsync(Guid.CreateVersion7(), recipient, limits)).IsAllowed);
    }

    [Fact]
    public async Task OneInvitersUsage_DoesNotCountAgainstAnother()
    {
        var limits = new CircleLimits { InvitationsPerInviterPerHour = 2 };
        var busy = Guid.CreateVersion7();
        var quiet = Guid.CreateVersion7();

        await using var provider = fixture.CreateProvider();
        var limiter = Limiter(provider, new MovableClock(CircleFactory.Now));

        Assert.True((await limiter.TryConsumeAsync(busy, Address(), limits)).IsAllowed);
        Assert.True((await limiter.TryConsumeAsync(busy, Address(), limits)).IsAllowed);
        Assert.False((await limiter.TryConsumeAsync(busy, Address(), limits)).IsAllowed);

        Assert.True((await limiter.TryConsumeAsync(quiet, Address(), limits)).IsAllowed);
    }

    private static EfInvitationRateLimiter Limiter(ServiceProvider provider, TimeProvider clock) =>
        new EfInvitationRateLimiter(provider.GetRequiredService<CirclesDbContext>(), clock);

    /// <summary>A hash of an address nothing else in the suite has used.</summary>
    private static string Address() => EmailAddress.HashOf($"{Guid.CreateVersion7():N}@example.test");
}
