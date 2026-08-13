using System.Diagnostics;
using CircleTools.Infrastructure.Outbox;

namespace CircleTools.Identity.Test;

/// <summary>
/// Covers the outbox row of TP-002: the structural guarantee behind NFR-005's timing rule.
/// </summary>
public sealed class OutboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Enqueue_RecordsMessageInOrder()
    {
        var outbox = new InMemoryOutbox();

        await outbox.EnqueueAsync(OutboxMessage.Create("mail.verification", "{}", Now));
        await outbox.EnqueueAsync(OutboxMessage.Create("mail.reset", "{}", Now.AddSeconds(1)));

        Assert.Collection(
            outbox.Messages,
            m => Assert.Equal("mail.verification", m.Type),
            m => Assert.Equal("mail.reset", m.Type));
    }

    [Fact]
    public void Create_AssignsTimeOrderedIds()
    {
        var first = OutboxMessage.Create("mail.verification", "{}", Now);
        var second = OutboxMessage.Create("mail.verification", "{}", Now);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(7, first.Id.Version);
    }

    [Fact]
    public async Task MailIsEnqueuedNotAwaited()
    {
        // NFR-005: the request path must only record the side effect. If sending ever moved
        // inline, the "address exists" path would take measurably longer than the "no account,
        // no mail" path — which is precisely the signal used to enumerate accounts. Enqueuing
        // must stay O(1) and indistinguishable between the two paths.
        var outbox = new InMemoryOutbox();
        var message = OutboxMessage.Create("mail.verification", "{}", Now);

        var stopwatch = Stopwatch.StartNew();
        await outbox.EnqueueAsync(message);
        stopwatch.Stop();

        Assert.Single(outbox.Messages);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(50),
            $"Enqueue took {stopwatch.ElapsedMilliseconds} ms — it must not perform the side effect.");
    }

    [Fact]
    public async Task Enqueue_IsSafeFromConcurrentRequests()
    {
        var outbox = new InMemoryOutbox();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(async i =>
            await outbox.EnqueueAsync(OutboxMessage.Create("mail.verification", $"{{\"i\":{i}}}", Now))));

        Assert.Equal(200, outbox.Messages.Count);
    }
}
