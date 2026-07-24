namespace AppSuite.SharedKernel;

/// <summary>
/// Base class for value objects. Value objects have no identity and are compared by
/// the equality of their components (see <see cref="GetEqualityComponents"/>).
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Returns the components that define equality for this value object.</summary>
    /// <returns>The ordered sequence of equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc />
    public bool Equals(ValueObject? other) =>
        other is not null && other.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>Determines whether two value objects are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if the operands are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>Determines whether two value objects are not equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if the operands differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
