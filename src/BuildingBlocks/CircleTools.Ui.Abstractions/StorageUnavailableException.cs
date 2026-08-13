namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Thrown when an <see cref="IKeyValueStore"/> refused a write that the caller cannot afford to
/// lose.
/// </summary>
/// <remarks>
/// The store itself never throws — it returns <see langword="false"/> (see
/// <see cref="IKeyValueStore.SetAsync"/>). This exception is what a caller raises after checking
/// that result, so the distinction between "a preference was not remembered" and "the user's data
/// was not saved" is made by the caller that knows which one it is holding.
/// </remarks>
public sealed class StorageUnavailableException : Exception
{
    /// <summary>Creates an exception with the default message.</summary>
    public StorageUnavailableException()
        : base("Local storage refused the write, so the data was not saved.")
    {
    }

    /// <summary>Creates an exception with a specific message.</summary>
    /// <param name="message">The message.</param>
    public StorageUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a specific message and cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public StorageUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
