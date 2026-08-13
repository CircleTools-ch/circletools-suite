using CircleTools.Circles.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Circles.Repository;

/// <summary>
/// The EF Core implementation of <see cref="ICircleRepository"/> over the <c>circles</c> schema.
/// </summary>
/// <param name="context">The circles database context.</param>
/// <remarks>
/// <para>
/// Circles are loaded <strong>whole</strong> — memberships, invitations and declines together.
/// That is not convenience: every rule the aggregate enforces is circle-scoped, and a partially
/// loaded circle cannot check "exactly one owner" or "one pending invitation per address". A circle
/// is capped at a hundred people and fifty invitations, so the whole thing is a cheap read.
/// </para>
/// <para>
/// Every write goes through one <c>SaveChangesAsync</c>, so a save is a transaction: a circle and
/// its owner's membership appear together or not at all (FR-016), and a deletion takes everything
/// inside the circle or nothing (FR-022).
/// </para>
/// </remarks>
public sealed class EfCircleRepository(CirclesDbContext context) : ICircleRepository
{
    /// <inheritdoc />
    public async Task<Circle?> FindAsync(Guid circleId, CancellationToken cancellationToken = default) =>
        await WholeAggregate().SingleOrDefaultAsync(c => c.Id == circleId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Circle>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await WholeAggregate()
            .Where(c => c.Memberships.Any(m => m.UserId == userId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountOwnedByAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Circles
            .CountAsync(
                c => c.Memberships.Any(m => m.UserId == userId && m.Role == CircleRole.Owner),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Counted as "circles this account owns that were created in the window" rather than from a
    /// separate creation log. A circle created and then transferred away stops counting, which is a
    /// known and accepted gap: this is an abuse limit (FEAT-003), transferring requires a second
    /// consenting account, and the alternative — retaining a creator identifier on every circle for
    /// the sole purpose of rate limiting — stores a person's data to answer a question that expires
    /// after a day (NFR-004).
    /// </remarks>
    public async Task<int> CountCreatedSinceAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await context.Circles
            .CountAsync(
                c => c.CreatedAt >= since
                    && c.Memberships.Any(m => m.UserId == userId && m.Role == CircleRole.Owner),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Circle?> FindByInvitationHandleAsync(
        string secretHandle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHandle);

        return await WholeAggregate()
            .SingleOrDefaultAsync(c => c.Invitations.Any(i => i.SecretHandle == secretHandle), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Circle>> ListWithInvitationForAsync(
        string emailHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailHash);

        return await WholeAggregate()
            .Where(c => c.Invitations.Any(i => i.EmailHash == emailHash))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Circle>> ListOwnedByAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await WholeAggregate()
            .Where(c => c.Memberships.Any(m => m.UserId == userId && m.Role == CircleRole.Owner))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        // The owner's membership is part of the same graph and therefore part of the same INSERT
        // batch: an ownerless circle can never be administered, transferred or deleted by anyone,
        // so it must not exist even for the width of a failed save (FR-016).
        await context.Circles.AddAsync(circle, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="DbUpdateConcurrencyException">
    /// Thrown when the circle changed since it was loaded. The caller's change is discarded whole —
    /// which is the point: a transfer that lost the race leaves the circle with the owner it has,
    /// never with two or none (FR-020).
    /// </exception>
    public async Task SaveAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        // Force an UPDATE of the root row even when only a child changed. Postgres bumps xmin per
        // row, and a membership's role changing does not touch the circle's row — so without this,
        // two admins transferring ownership at the same time would both succeed and the last write
        // would silently win. Marking the root modified makes the concurrency token cover the whole
        // aggregate, which is the granularity every invariant here is stated at.
        context.Entry(circle).State = EntityState.Modified;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        // Memberships, managed-member profiles, pending invitations and decline records go with it,
        // by the cascade the model declares. One transaction, no soft-delete state to reason about
        // afterwards (FR-022).
        context.Circles.Remove(circle);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<Circle> WholeAggregate() =>
        context.Circles
            .Include(c => c.Memberships)
            .Include(c => c.Invitations)
            .Include(c => c.Declines);
}
