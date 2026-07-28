using AppSuite.ApiClient;
using Microsoft.Extensions.Localization;

namespace AppSuite.Main.Ui.Api;

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
    /// These <em>are</em> shown, unlike <c>detail</c>: they are almost always the password-policy
    /// text from Identity, and telling someone their password is too short in English beats telling
    /// them nothing at all in three languages. Localizing them properly needs the API to return
    /// codes rather than prose — a change worth making when the policy grows past one rule.
    /// </remarks>
    /// <param name="error">The failure.</param>
    /// <returns>The messages, or an empty list.</returns>
    public static IReadOnlyList<string> Details(ApiError? error) =>
        error is null ? [] : [.. error.Errors.SelectMany(e => e.Value)];
}
