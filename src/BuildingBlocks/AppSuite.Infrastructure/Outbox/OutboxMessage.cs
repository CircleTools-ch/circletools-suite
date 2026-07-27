using AppSuite.SharedKernel;

namespace AppSuite.Infrastructure.Outbox;

/// <summary>
/// A side effect recorded during a request and dispatched afterwards — chiefly outbound mail.
/// </summary>
/// <param name="Id">Time-ordered identity (ADR-0008).</param>
/// <param name="Type">Discriminator telling the dispatcher how to read <paramref name="Payload"/>.</param>
/// <param name="Payload">Serialized message body.</param>
/// <param name="EnqueuedAt">When the request enqueued it.</param>
public sealed record OutboxMessage(Guid Id, string Type, string Payload, DateTimeOffset EnqueuedAt)
{
    /// <summary>Creates a message with a fresh time-ordered id.</summary>
    /// <param name="type">Discriminator for the dispatcher.</param>
    /// <param name="payload">Serialized message body.</param>
    /// <param name="enqueuedAt">The current time.</param>
    /// <returns>A new <see cref="OutboxMessage"/>.</returns>
    public static OutboxMessage Create(string type, string payload, DateTimeOffset enqueuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payload);

        return new OutboxMessage(GuidV7.New(), type, payload, enqueuedAt);
    }
}
