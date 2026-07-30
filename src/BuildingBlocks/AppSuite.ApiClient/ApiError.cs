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
/// <param name="Code">
/// The refusal's stable machine code, when the endpoint supplied one — <c>circle_full</c>,
/// <c>owner_must_transfer_ownership</c> and so on. <b>This is the part that is for display</b>, by
/// way of a lookup into the resource files: <see cref="ApiFailure"/> is too coarse to say anything
/// useful about a <c>409</c>, and <paramref name="Detail"/> is English prose the product cannot show
/// (NFR-003, ADR-0011). Null when the endpoint answered without one.
/// </param>
public sealed record ApiError(
    ApiFailure Failure,
    string? Detail = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null,
    TimeSpan? RetryAfter = null,
    string? Code = null)
{
    /// <summary>Per-field validation messages; never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; } =
        Errors ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}
