namespace AppSuite.Infrastructure.Outbox;

/// <summary>
/// Records side effects for dispatch <em>after</em> the response is written.
/// </summary>
/// <remarks>
/// This is a security control, not only a performance one. NFR-005 requires that
/// address-accepting endpoints respond identically whether or not an account exists —
/// including in <em>timing</em>. Sending mail inline would make the "address exists" path
/// measurably slower than the "no account, no mail" path, which is exactly the signal an
/// attacker measures to enumerate accounts. Enqueuing is O(1) and identical on both paths.
/// <para>
/// Implementations must therefore never perform the side effect itself.
/// </para>
/// </remarks>
public interface IOutbox
{
    /// <summary>Records a message for later dispatch. Must not perform the side effect.</summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the message is recorded.</returns>
    ValueTask EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
