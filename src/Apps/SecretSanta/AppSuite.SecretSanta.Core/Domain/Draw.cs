using AppSuite.SharedKernel;

namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>
/// A completed draw: the set of <see cref="Assignment"/>s produced at a point in time.
/// Owned by the <see cref="Group"/> aggregate root and kept in the group's draw history.
/// </summary>
public sealed class Draw : Entity
{
    private readonly List<Assignment> _assignments;

    private Draw(Guid id, DateTimeOffset createdAt, List<Assignment> assignments)
    {
        Id = id;
        CreatedAt = createdAt;
        _assignments = assignments;
    }

    /// <summary>Gets the instant the draw was produced.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the assignments that make up this draw.</summary>
    public IReadOnlyList<Assignment> Assignments => _assignments.AsReadOnly();

    internal static Draw Create(IReadOnlyList<Assignment> assignments, DateTimeOffset createdAt) =>
        new(GuidV7.New(), createdAt, [.. assignments]);

    internal static Draw Restore(Guid id, DateTimeOffset createdAt, IReadOnlyList<Assignment> assignments) =>
        new(id, createdAt, [.. assignments]);

    /// <summary>Gets the recipient assigned to the given giver, or <see langword="null"/> if none.</summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <returns>The recipient member identity, or <see langword="null"/>.</returns>
    public Guid? RecipientFor(Guid giverMemberId) =>
        _assignments.FirstOrDefault(a => a.GiverMemberId == giverMemberId)?.RecipientMemberId;

    /// <summary>Projects this draw to a giver-to-recipient map.</summary>
    /// <returns>A dictionary mapping each giver to their recipient.</returns>
    public IReadOnlyDictionary<Guid, Guid> ToMap() =>
        _assignments.ToDictionary(a => a.GiverMemberId, a => a.RecipientMemberId);
}
