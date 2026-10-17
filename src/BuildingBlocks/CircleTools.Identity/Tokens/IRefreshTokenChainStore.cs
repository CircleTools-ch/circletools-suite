namespace CircleTools.Identity.Tokens;

/// <summary>
/// Persistence port for <see cref="RefreshTokenChain"/>. Implemented by the app's EF adapter.
/// </summary>
public interface IRefreshTokenChainStore
{
    /// <summary>
    /// Loads the chain containing a given token handle, with all of its tokens.
    /// </summary>
    /// <param name="handle">The stored handle (never the client's secret).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chain, or <c>null</c> if no token has that handle.</returns>
    /// <remarks>
    /// The whole chain is loaded because reuse detection needs the token's consumption state
    /// <em>and</em> its successor — a lookup of the single token would be enough to accept a
    /// replay but not to recognise one.
    /// </remarks>
    Task<RefreshTokenChain?> FindByTokenHandleAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>Adds a newly started chain.</summary>
    /// <param name="chain">The chain to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the chain is tracked.</returns>
    Task AddAsync(RefreshTokenChain chain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live chain belonging to a user — used by password reset (FR-009) and
    /// account deletion (FR-015), both of which must end all sessions everywhere.
    /// </summary>
    /// <param name="userId">The user whose sessions end.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of chains revoked.</returns>
    Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live chain belonging to a user except one — used by a password change (FR-049), which
    /// ends the other sessions and keeps the one that made the change (ADR-0079).
    /// </summary>
    /// <param name="userId">The user whose other sessions end.</param>
    /// <param name="keepChainId">The chain to leave live.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of chains revoked.</returns>
    Task<int> RevokeAllForUserExceptAsync(Guid userId, Guid keepChainId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Counts a user's live sessions — reported in their data export (FR-014).</summary>
    /// <param name="userId">The user to count sessions for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many chains are still live.</returns>
    Task<int> CountActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's unrevoked chains, with their tokens — the signed-in devices (ADR-0079 amendment).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chains, in no particular order.</returns>
    Task<IReadOnlyList<RefreshTokenChain>> ListUnrevokedForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Finds a chain by its id.</summary>
    /// <param name="chainId">The chain id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chain, or <c>null</c>.</returns>
    Task<RefreshTokenChain?> FindByIdAsync(Guid chainId, CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once changes are written.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
