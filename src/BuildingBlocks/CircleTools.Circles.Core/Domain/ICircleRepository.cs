namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// The Circles context's persistence port. Circles are loaded and saved whole: every invariant is
/// circle-scoped, and the aggregate is small enough that partial loading would buy nothing and cost
/// the ability to enforce them.
/// </summary>
public interface ICircleRepository
{
    /// <summary>Loads a circle by its identity.</summary>
    /// <param name="circleId">The circle to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circle, or <c>null</c> when there is none.</returns>
    Task<Circle?> FindAsync(Guid circleId, CancellationToken cancellationToken = default);

    /// <summary>Loads every circle an account belongs to.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circles, in creation order.</returns>
    Task<IReadOnlyList<Circle>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Counts the circles an account owns, for the ownership cap.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many circles they own.</returns>
    Task<int> CountOwnedByAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Counts the circles an account created since an instant, for the creation rate limit.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="since">The start of the window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many they created in the window.</returns>
    Task<int> CountCreatedSinceAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the circle holding the invitation whose link secret hashes to a handle.</summary>
    /// <param name="secretHandle">The stored handle computed from the secret in the link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circle, or <c>null</c> when no invitation matches.</returns>
    Task<Circle?> FindByInvitationHandleAsync(
        string secretHandle,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the circles with an invitation pending for an address.</summary>
    /// <param name="emailHash">The hash of the address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circles awaiting that address's answer.</returns>
    Task<IReadOnlyList<Circle>> ListWithInvitationForAsync(
        string emailHash,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the circles an account owns, to answer FR-015's deletion guard and FR-021.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circles they own.</returns>
    Task<IReadOnlyList<Circle>> ListOwnedByAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new circle.</summary>
    /// <param name="circle">The circle to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once it is persisted.</returns>
    Task AddAsync(Circle circle, CancellationToken cancellationToken = default);

    /// <summary>Persists changes made to a circle.</summary>
    /// <param name="circle">The circle to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the changes are persisted.</returns>
    Task SaveAsync(Circle circle, CancellationToken cancellationToken = default);

    /// <summary>Deletes a circle with everything inside it, in one transaction.</summary>
    /// <param name="circle">The circle to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once it is gone.</returns>
    Task DeleteAsync(Circle circle, CancellationToken cancellationToken = default);
}
