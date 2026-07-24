using Microsoft.EntityFrameworkCore;
using Walf.Main.Service.Core.Domain;

namespace Walf.Main.Service.Repository;

/// <summary>PostgreSQL adapter implementing <see cref="IHeartbeatRepository"/> via EF Core.</summary>
/// <param name="dbContext">The Main database context.</param>
public sealed class HeartbeatRepository(MainDbContext dbContext) : IHeartbeatRepository
{
    /// <inheritdoc />
    public async Task AddAsync(Heartbeat heartbeat, CancellationToken cancellationToken)
    {
        await dbContext.Heartbeats.AddAsync(heartbeat, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Heartbeat>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Heartbeats
            .AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
