using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository.Test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CircleTools.Circles.Repository.Test;

/// <summary>
/// The retention sweep over the circles schema (NFR-004), against real Postgres.
/// </summary>
/// <remarks>
/// The clock is injected, so the fourteen-day expiry and the seven-day cooldown are exercised in
/// milliseconds rather than by waiting or by shrinking the windows to something a test can outlast.
/// </remarks>
[Collection(SharedCirclesDatabase.CollectionName)]
public sealed class InvitationPurgeTests(CirclesPostgresFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ExpiredInvitations_ArePurged_AndPendingOnesAreNot()
    {
        // A pending invitation is the only place M2 holds an address belonging to someone who has
        // not agreed to anything, so it must not outlive the window it was acceptable for.
        // Fifteen days apart, in two circles. Two, because the aggregate drops its own expired
        // invitations whenever it is touched — the whole point of the sweep is the circles nobody
        // opens, so the stale one must not be routed through a load that would tidy it up first.
        var later = CircleFactory.Now.AddDays(15);
        var owner = Guid.CreateVersion7();

        var quiet = CircleFactory.WithOwner(owner, name: "Quiet");
        var stale = CircleFactory.Invite(quiet, owner, "dora@example.test");

        var active = CircleFactory.WithOwner(owner, name: "Active", now: later);
        var fresh = CircleFactory.Invite(active, owner, "chiara@example.test", now: later);

        await using var provider = fixture.CreateProvider();
        var repository = provider.GetRequiredService<ICircleRepository>();
        await repository.AddAsync(quiet);
        await repository.AddAsync(active);

        var purged = await Purge(provider, later).PurgeAsync();

        Assert.True(purged >= 1);

        await using var verifying = fixture.CreateProvider();
        var context = verifying.GetRequiredService<CirclesDbContext>();
        var remaining = await context.Set<Invitation>().AsNoTracking().ToListAsync();

        Assert.DoesNotContain(stale.Id, remaining.Select(i => i.Id));
        Assert.Contains(fresh.Id, remaining.Select(i => i.Id));
    }

    [Fact]
    public async Task ADeclineIsSwept_OnceItSuppressesNothing()
    {
        var owner = Guid.CreateVersion7();
        var circle = CircleFactory.WithOwner(owner);
        var invitation = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(invitation.Id, CircleFactory.Now).IsSuccess);

        await using var provider = fixture.CreateProvider();
        await provider.GetRequiredService<ICircleRepository>().AddAsync(circle);

        // Inside the seven-day cooldown the record is still doing its job.
        await Purge(provider, CircleFactory.Now.AddDays(6)).PurgeAsync();

        await using (var checking = fixture.CreateProvider())
        {
            var reloaded = await checking.GetRequiredService<ICircleRepository>().FindAsync(circle.Id);
            Assert.Single(reloaded!.Declines);
        }

        await Purge(provider, CircleFactory.Now.AddDays(8)).PurgeAsync();

        await using var verifying = fixture.CreateProvider();
        var settled = await verifying.GetRequiredService<ICircleRepository>().FindAsync(circle.Id);
        Assert.Empty(settled!.Declines);
    }

    [Fact]
    public async Task CountedInvitationAttempts_AreSweptOnceNoLimitCountsThem()
    {
        var inviter = Guid.CreateVersion7();
        var hash = TestEmailHasher.Instance.Hash("dora@example.test");

        await using var provider = fixture.CreateProvider();
        var limiter = new EfInvitationRateLimiter(
            provider.GetRequiredService<CirclesDbContext>(),
            new FixedClock(CircleFactory.Now));

        Assert.True((await limiter.TryConsumeAsync(inviter, hash, CircleFactory.Limits())).IsAllowed);

        await Purge(provider, CircleFactory.Now.AddHours(2)).PurgeAsync();

        await using (var checking = fixture.CreateProvider())
        {
            var context = checking.GetRequiredService<CirclesDbContext>();
            Assert.Single(await context.InvitationAttempts.AsNoTracking()
                .Where(a => a.InviterUserId == inviter).ToListAsync());
        }

        await Purge(provider, CircleFactory.Now.AddDays(2)).PurgeAsync();

        await using var verifying = fixture.CreateProvider();
        var settled = verifying.GetRequiredService<CirclesDbContext>();
        Assert.Empty(await settled.InvitationAttempts.AsNoTracking()
            .Where(a => a.InviterUserId == inviter).ToListAsync());
    }

    [Fact]
    public async Task Purge_IsSafeWhenNothingHasExpired()
    {
        await using var provider = fixture.CreateProvider();

        // Long before anything in this database was written.
        Assert.Equal(0, await Purge(provider, CircleFactory.Now.AddYears(-5)).PurgeAsync());
    }

    private static CirclesPurge Purge(ServiceProvider provider, DateTimeOffset now) =>
        new(
            provider.GetRequiredService<CirclesDbContext>(),
            Options.Create(new CircleLimits()),
            new FixedClock(now),
            NullLogger<CirclesPurge>.Instance);
}
