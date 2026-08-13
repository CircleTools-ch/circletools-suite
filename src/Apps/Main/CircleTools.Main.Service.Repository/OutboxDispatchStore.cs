using CircleTools.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service.Repository;

/// <summary>EF Core adapter for <see cref="IOutboxDispatchStore"/>.</summary>
/// <param name="context">The Main database context.</param>
public sealed class OutboxDispatchStore(MainDbContext context) : IOutboxDispatchStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        int batchSize,
        int maxAttempts,
        CancellationToken cancellationToken = default) =>
        await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < maxAttempts)
            .OrderBy(m => m.EnqueuedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
