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
    /// <summary>Gets when the message was successfully dispatched, or <c>null</c> while pending.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Gets how many dispatch attempts have been made.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the last failure reason, for diagnosis.</summary>
    public string? LastError { get; private set; }

    /// <summary>Gets a value indicating whether the message still needs dispatching.</summary>
    public bool IsPending => ProcessedAt is null;

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

    /// <summary>Marks the message dispatched.</summary>
    /// <param name="at">The current time.</param>
    public void MarkProcessed(DateTimeOffset at)
    {
        Attempts++;
        ProcessedAt = at;
        LastError = null;
    }

    /// <summary>
    /// Records a failed attempt, leaving the message pending so it is retried.
    /// </summary>
    /// <param name="error">Why the attempt failed.</param>
    /// <remarks>
    /// Retrying matters more here than elsewhere: a lost verification or reset mail is a user who
    /// cannot get into their account (FR-007, FR-009).
    /// </remarks>
    public void MarkFailed(string error)
    {
        Attempts++;

        // Truncated: this is a diagnostic breadcrumb, not a log sink, and the column is bounded.
        LastError = error.Length > 500 ? error[..500] : error;
    }
}
