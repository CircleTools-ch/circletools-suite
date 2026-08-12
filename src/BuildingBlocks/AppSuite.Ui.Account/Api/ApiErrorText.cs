using AppSuite.ApiClient;
using Microsoft.Extensions.Localization;

namespace AppSuite.Ui.Account.Api;

/// <summary>
/// Turns an <see cref="ApiError"/> into a sentence in the user's language.
/// </summary>
/// <remarks>
/// The server's own <c>detail</c> is never shown. It is English, and the product is not (NFR-003);
/// worse, several endpoints answer deliberately vaguely so they cannot be used to discover which
/// addresses are registered (NFR-005), and surfacing those strings would slowly undo that.
/// </remarks>
public static class ApiErrorText
{
    /// <summary>
    /// Per-field messages the API reports as codes rather than prose, mapped to resource keys.
    /// </summary>
    /// <remarks>
    /// The password-policy codes come from ASP.NET Core Identity, except <c>BreachedPassword</c>,
    /// which is the suite's own validator (ADR-0027). Only codes the configured policy can actually
    /// produce are listed: FR-007 turns the composition rules off, so <c>PasswordRequiresDigit</c>
    /// and its siblings can never arrive.
    /// </remarks>
    private static readonly Dictionary<string, string> _localizableCodes = new(StringComparer.Ordinal)
    {
        ["PasswordTooShort"] = "PasswordErrorTooShort",
        ["BreachedPassword"] = "PasswordErrorBreached",
        ["InvalidToken"] = "PasswordErrorInvalidToken",
    };

    /// <summary>Describes a failure.</summary>
    /// <param name="localizer">The page's string localizer.</param>
    /// <param name="error">The failure, or <see langword="null"/> for none.</param>
    /// <returns>A localized sentence, or an empty string when there is nothing to report.</returns>
    public static string Describe(IStringLocalizer localizer, ApiError? error)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        if (error is null)
        {
            return string.Empty;
        }

        return error.Failure switch
        {
            ApiFailure.Validation => localizer["ErrorValidation"],
            ApiFailure.Unauthorized => localizer["ErrorUnauthorized"],
            ApiFailure.Forbidden => localizer["ErrorForbidden"],
            ApiFailure.NotFound => localizer["ErrorNotFound"],
            ApiFailure.Conflict => localizer["ErrorConflict"],
            ApiFailure.RateLimited => localizer["ErrorRateLimited"],
            ApiFailure.Unreachable => localizer["ErrorUnreachable"],
            _ => localizer["ErrorUnexpected"],
        };
    }

    /// <summary>
    /// Lists the per-field messages a validation failure carried, for display under the summary.
    /// </summary>
    /// <remarks>
    /// These <em>are</em> shown, unlike <c>detail</c> — telling someone why their password was
    /// refused is the point of the rule (FR-007). The API sends password-policy failures as
    /// <strong>codes</strong> so they can be said in the user's language (NFR-003); anything that is
    /// not a known code is passed through unchanged, which is what the endpoints still answering in
    /// prose rely on.
    /// </remarks>
    /// <param name="localizer">The page's string localizer.</param>
    /// <param name="error">The failure.</param>
    /// <returns>The messages, or an empty list.</returns>
    public static IReadOnlyList<string> Details(IStringLocalizer localizer, ApiError? error)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return error is null
            ? []
            : [.. error.Errors
                .SelectMany(e => e.Value)
                .Select(message => _localizableCodes.TryGetValue(message, out var key)
                    ? localizer[key].Value
                    : message)];
    }
}
