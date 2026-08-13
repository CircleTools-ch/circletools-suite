namespace CircleTools.SharedKernel;

/// <summary>
/// Base class for entities. Entities have a stable identity (<see cref="Id"/>) and
/// are compared by that identity rather than by their attribute values.
/// </summary>
public abstract class Entity
{
    /// <summary>Gets the unique identity of the entity.</summary>
    public Guid Id { get; protected init; }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
