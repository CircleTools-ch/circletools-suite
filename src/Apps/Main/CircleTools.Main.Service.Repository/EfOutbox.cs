using CircleTools.Infrastructure.Outbox;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Durable <see cref="IOutbox"/> backed by the Main database.
/// </summary>
/// <param name="context">The Main database context.</param>
/// <remarks>
/// <para>
/// The write is still cheap and constant-time — an insert, never an SMTP round-trip — which is
/// what NFR-005's timing requirement actually depends on.
/// </para>
/// <para>
/// <strong>Known gap:</strong> this commits in its own transaction rather than sharing the
/// caller's. <c>UserManager</c> owns its saves, so registration currently writes the account and
/// then the message as two commits; a crash between them loses a verification mail and leaves an
/// account that cannot verify. Recovering that needs a shared transaction or unit of work around
/// the endpoint — a follow-up, not something the outbox can fix alone.
/// </para>
/// </remarks>
public sealed class EfOutbox(MainDbContext context) : IOutbox
{
    /// <inheritdoc />
    public async ValueTask EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await context.OutboxMessages.AddAsync(message, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
