using CircleTools.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Covers the 7-day purge of outbox messages (NFR-004) against a real Postgres.
/// </summary>
/// <remarks>
/// A mail payload carries the recipient's address in plain text, and the privacy policy promises it
/// is gone within a week of sending — these tests hold the code to that promise.
/// </remarks>
[Collection(SharedPostgresDatabase.CollectionName)]
public sealed class OutboxPurgeTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<Guid> AddMessageAsync(
        MainDbContext context,
        DateTimeOffset enqueuedAt,
        DateTimeOffset? processedAt)
    {
        var message = OutboxMessage.Create("mail.circle-invitation", """{"email":"purge@example.test"}""", enqueuedAt);

        if (processedAt is { } at)
        {
            message.MarkProcessed(at);
        }

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();
        return message.Id;
    }

    private static OutboxPurge Purge(MainDbContext context, DateTimeOffset now) =>
        new(
            context,
            Options.Create(new RetentionOptions()),
            new FixedClock(now),
            NullLogger<OutboxPurge>.Instance);

    private static Task<bool> ExistsAsync(MainDbContext context, Guid id) =>
        context.OutboxMessages.AsNoTracking().AnyAsync(m => m.Id == id);

    [Fact]
    public async Task Purge_RemovesMessagesSentBeforeTheWindow()
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var old = await AddMessageAsync(context, Now.AddDays(-9), processedAt: Now.AddDays(-8));

        var purged = await Purge(context, Now).PurgeAsync();

        Assert.True(purged >= 1);
        Assert.False(await ExistsAsync(context, old));
    }

    [Fact]
    public async Task Purge_KeepsMessagesSentInsideTheWindow()
    {
        // Enqueued long ago but only just sent (a retried mail): the window runs from sending.
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var recent = await AddMessageAsync(context, Now.AddDays(-10), processedAt: Now.AddDays(-6));

        await Purge(context, Now).PurgeAsync();

        Assert.True(await ExistsAsync(context, recent));
    }

    [Fact]
    public async Task Purge_RemovesMessagesNeverSentBeforeTheWindow()
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var stale = await AddMessageAsync(context, Now.AddDays(-8), processedAt: null);

        await Purge(context, Now).PurgeAsync();

        Assert.False(await ExistsAsync(context, stale));
    }

    [Fact]
    public async Task Purge_KeepsPendingMessagesInsideTheWindow()
    {
        // A mail still waiting to go out must never be purged before it had its chance.
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var pending = await AddMessageAsync(context, Now.AddMinutes(-1), processedAt: null);

        await Purge(context, Now).PurgeAsync();

        Assert.True(await ExistsAsync(context, pending));
    }
}
