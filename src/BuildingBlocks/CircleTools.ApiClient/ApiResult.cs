namespace CircleTools.ApiClient;

/// <summary>
/// The outcome of an API call that returns no body.
/// </summary>
/// <remarks>
/// Expected failures are return values, not exceptions — see the coding guidelines, "no
/// control-flow-by-exception". A page renders an error; it does not catch one.
/// </remarks>
public class ApiResult
{
    /// <summary>Initialises a result.</summary>
    /// <param name="error">The failure, or <see langword="null"/> for success.</param>
    protected ApiResult(ApiError? error) => Error = error;

    /// <summary>The failure, or <see langword="null"/> when the call succeeded.</summary>
    public ApiError? Error { get; }

    /// <summary>Whether the call succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>A successful call with no body.</summary>
    /// <returns>A successful result.</returns>
    public static ApiResult Ok() => new(null);

    /// <summary>A failed call with no body.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A failed result.</returns>
    public static ApiResult Failed(ApiError error) => new(error);

    /// <summary>A successful call carrying a value.</summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="value">The response.</param>
    /// <returns>A successful result.</returns>
    public static ApiResult<T> Ok<T>(T value) => new(value, null);

    /// <summary>A failed call that would have carried a value.</summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="error">The failure.</param>
    /// <returns>A failed result.</returns>
    public static ApiResult<T> Failed<T>(ApiError error) => new(default, error);
}

/// <summary>The outcome of an API call that returns a body.</summary>
/// <typeparam name="T">The response type.</typeparam>
public sealed class ApiResult<T> : ApiResult
{
    internal ApiResult(T? value, ApiError? error)
        : base(error) => Value = value;

    /// <summary>The response, or <see langword="null"/> when the call failed.</summary>
    public T? Value { get; }
}
