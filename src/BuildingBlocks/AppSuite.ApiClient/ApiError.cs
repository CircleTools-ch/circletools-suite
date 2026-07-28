namespace AppSuite.ApiClient;

/// <summary>
/// Why an API call did not succeed, in terms the UI can act on.
/// </summary>
/// <remarks>
/// Deliberately coarse. The server is enumeration-resistant (NFR-005) and answers "no such account"
/// and "wrong password" identically, so a finer-grained set would be inventing distinctions the
/// contract does not make.
/// </remarks>
public enum ApiFailure
{
    /// <summary>The request was rejected as invalid; see <see cref="ApiError.Errors"/>.</summary>
    Validation,

    /// <summary>No valid session, or the credentials were refused.</summary>
    Unauthorized,

    /// <summary>Authenticated, but not allowed to do this.</summary>
    Forbidden,

    /// <summary>The addressed thing does not exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state.</summary>
    Conflict,

    /// <summary>Rate-limited; see <see cref="ApiError.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The API could not be reached at all — offline, DNS, CORS, TLS.</summary>
    Unreachable,

    /// <summary>Anything else, including 5xx.</summary>
    Unexpected,
}

/// <summary>A failed API call.</summary>
/// <param name="Failure">The failure category the UI switches on.</param>
/// <param name="Detail">
/// The server's message, when it supplied one. <b>Not for display</b> — server messages are English
/// and the product is multilingual (NFR-003); the UI maps <paramref name="Failure"/> to a localized
/// string and keeps this for diagnostics.
/// </param>
/// <param name="Errors">Per-field validation messages, keyed by field name. Empty unless
/// <paramref name="Failure"/> is <see cref="ApiFailure.Validation"/>.</param>
/// <param name="RetryAfter">How long to wait, when the server said so.</param>
public sealed record ApiError(
    ApiFailure Failure,
    string? Detail = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null,
    TimeSpan? RetryAfter = null)
{
    /// <summary>Per-field validation messages; never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; } =
        Errors ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}
