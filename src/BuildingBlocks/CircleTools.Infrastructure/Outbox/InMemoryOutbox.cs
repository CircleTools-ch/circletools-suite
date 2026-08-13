using System.Collections.Concurrent;

namespace CircleTools.Infrastructure.Outbox;

/// <summary>
/// An <see cref="IOutbox"/> that keeps messages in memory.
/// </summary>
/// <remarks>
/// Intended for tests and for local development before the EF-backed outbox exists. Messages
/// do not survive a restart, so this is not suitable for production, where a lost verification
/// or password-reset mail is a user locked out.
/// </remarks>
public sealed class InMemoryOutbox : IOutbox
{
    private readonly ConcurrentQueue<OutboxMessage> _messages = new();

    /// <summary>Gets the messages enqueued so far, in order.</summary>
    public IReadOnlyCollection<OutboxMessage> Messages => [.. _messages];

    /// <inheritdoc />
    public ValueTask EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        _messages.Enqueue(message);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes every recorded message.</summary>
    public void Clear() => _messages.Clear();
}
