using System.Diagnostics.CodeAnalysis;

namespace AppSuite.Circles.Core.Domain;

/// <summary>The outcome of a circle operation that produces no value: success, or a refusal.</summary>
public sealed class CircleResult
{
    private CircleResult(CircleError? error) => Error = error;

    /// <summary>Gets the successful outcome.</summary>
    public static CircleResult Success { get; } = new(error: null);

    /// <summary>Gets the reason the operation was refused, or <c>null</c> when it succeeded.</summary>
    public CircleError? Error { get; }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Creates a refused outcome.</summary>
    /// <param name="error">The reason for the refusal.</param>
    /// <returns>The refused outcome.</returns>
    public static CircleResult Refused(CircleError error) => new(error);
}

/// <summary>The outcome of a circle operation that produces a value on success.</summary>
/// <typeparam name="TValue">The type produced on success.</typeparam>
public sealed class CircleResult<TValue>
    where TValue : class
{
    private CircleResult(TValue? value, CircleError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Gets the produced value, or <c>null</c> when the operation was refused.</summary>
    public TValue? Value { get; }

    /// <summary>Gets the reason the operation was refused, or <c>null</c> when it succeeded.</summary>
    public CircleError? Error { get; }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Error is null;

    internal static CircleResult<TValue> Create(TValue? value, CircleError? error) => new(value, error);
}

/// <summary>Creates <see cref="CircleResult{TValue}"/> outcomes.</summary>
public static class CircleResults
{
    /// <summary>Creates a successful outcome.</summary>
    /// <typeparam name="TValue">The type produced.</typeparam>
    /// <param name="value">The produced value.</param>
    /// <returns>The successful outcome.</returns>
    public static CircleResult<TValue> Ok<TValue>(TValue value)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return CircleResult<TValue>.Create(value, error: null);
    }

    /// <summary>Creates a refused outcome.</summary>
    /// <typeparam name="TValue">The type that would have been produced.</typeparam>
    /// <param name="error">The reason for the refusal.</param>
    /// <returns>The refused outcome.</returns>
    public static CircleResult<TValue> Refused<TValue>(CircleError error)
        where TValue : class =>
        CircleResult<TValue>.Create(value: null, error);
}
