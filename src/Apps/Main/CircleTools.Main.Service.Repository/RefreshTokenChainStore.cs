using CircleTools.Identity.Tokens;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service.Repository;

/// <summary>EF Core adapter for <see cref="IRefreshTokenChainStore"/>.</summary>
/// <param name="context">The Main database context.</param>
public sealed class RefreshTokenChainStore(MainDbContext context) : IRefreshTokenChainStore
{
    /// <inheritdoc />
    public async Task<RefreshTokenChain?> FindByTokenHandleAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        return await context.RefreshTokenChains
            .Include(c => c.Tokens)
            .SingleOrDefaultAsync(c => c.Tokens.Any(t => t.Handle == handle), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(RefreshTokenChain chain, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);

        await context.RefreshTokenChains.AddAsync(chain, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Loaded rather than bulk-updated so revocation goes through the aggregate's own rule
        // (Revoke keeps the first revocation time). Sessions per user are few.
        var chains = await context.RefreshTokenChains
            .Where(c => c.UserId == userId && c.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var chain in chains)
        {
            chain.Revoke(now);
        }

        return chains.Count;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForUserExceptAsync(
        Guid userId,
        Guid keepChainId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var chains = await context.RefreshTokenChains
            .Where(c => c.UserId == userId && c.RevokedAt == null && c.Id != keepChainId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var chain in chains)
        {
            chain.Revoke(now);
        }

        return chains.Count;
    }

    /// <inheritdoc />
    public async Task<int> CountActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenChains
            .CountAsync(c => c.UserId == userId && c.RevokedAt == null, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
