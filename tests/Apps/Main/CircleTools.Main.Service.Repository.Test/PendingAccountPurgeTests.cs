using CircleTools.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Covers the 30-day purge of unverified accounts (NFR-004) against a real Postgres.
/// </summary>
/// <remarks>
/// The clock is injected, so the 30-day boundary is exercised in milliseconds rather than by
/// waiting a month or by fudging the retention window down to something the test can outlast.
/// </remarks>
[Collection(SharedPostgresDatabase.CollectionName)]
public sealed class PendingAccountPurgeTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<Guid> AddUserAsync(
        MainDbContext context,
        bool emailConfirmed,
        DateTimeOffset createdAt)
    {
        var id = Guid.CreateVersion7();
        var email = $"purge-{id:N}@example.test";

        context.Users.Add(new CircleToolsUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = "Anna",
            PreferredLanguage = "de-CH",
            EmailConfirmed = emailConfirmed,
            CreatedAt = createdAt,
            SecurityStamp = Guid.CreateVersion7().ToString(),
        });

        await context.SaveChangesAsync();
        return id;
    }

    private static PendingAccountPurge Purge(MainDbContext context, DateTimeOffset now) =>
        new(
            context,
            Options.Create(new RetentionOptions()),
            new FixedClock(now),
            NullLogger<PendingAccountPurge>.Instance);

    [Fact]
    public async Task Purge_RemovesUnverifiedAccountsPastTheWindow()
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var expired = await AddUserAsync(context, emailConfirmed: false, Now.AddDays(-31));

        var purged = await Purge(context, Now).PurgeAsync();

        Assert.True(purged >= 1);
        Assert.Null(await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == expired));
    }

    [Fact]
    public async Task Purge_KeepsUnverifiedAccountsInsideTheWindow()
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var young = await AddUserAsync(context, emailConfirmed: false, Now.AddDays(-29));

        await Purge(context, Now).PurgeAsync();

        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == young));
    }

    [Fact]
    public async Task Purge_NeverTouchesVerifiedAccountsHoweverOld()
    {
        // Retention applies to accounts that were never proven, not to inactive ones. Deleting a
        // long-standing verified account would be catastrophic and silent.
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var ancient = await AddUserAsync(context, emailConfirmed: true, Now.AddYears(-5));

        await Purge(context, Now).PurgeAsync();

        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == ancient));
    }

    [Fact]
    public async Task Purge_IsSafeWhenNothingIsExpired()
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        await AddUserAsync(context, emailConfirmed: false, Now.AddDays(-1));

        var purged = await Purge(context, Now.AddDays(-365)).PurgeAsync();

        Assert.Equal(0, purged);
    }
}
