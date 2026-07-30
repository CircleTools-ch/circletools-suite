using AppSuite.ApiClient;
using Microsoft.Extensions.Localization;

namespace AppSuite.Main.Ui.Api;

/// <summary>
/// Turns a circle refusal into a sentence in the user's language (FEAT-003, NFR-003).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ApiErrorText"/> describes failures by <em>status</em>, which is all M1's endpoints
/// needed. It is not enough here: almost every interesting circle refusal is a <c>409</c>, and
/// "there was a conflict" is useless when what the user needs to hear is "hand ownership to somebody
/// else first". So this maps the stable <c>code</c> the API sends (ADR-0011) to a resource key, and
/// falls back to the status-based text when a refusal carries no code.
/// </para>
/// <para>
/// <strong>Unknown codes fall back rather than being shown.</strong> A code that arrives without a
/// resource key means the API grew a refusal the UI has not caught up with; showing the raw
/// <c>circle_full</c> to a user would be worse than showing the generic sentence, and showing the
/// server's English title would break NFR-003.
/// </para>
/// </remarks>
public static class CircleErrorText
{
    /// <summary>
    /// The refusal codes <c>CircleProblems</c> can send, mapped to resource keys.
    /// </summary>
    /// <remarks>
    /// Deliberately exhaustive over that mapping — a missing entry is a user-visible regression, and
    /// <c>CircleErrorTextTests</c> asserts every code is covered so the two cannot drift apart.
    /// </remarks>
    private static readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal)
    {
        ["circle_not_found"] = "CircleErrorNotFound",
        ["invitation_not_found"] = "CircleErrorInvitationNotFound",
        ["invitation_expired"] = "CircleErrorInvitationExpired",
        ["role_not_permitted"] = "CircleErrorRoleNotPermitted",
        ["email_not_verified"] = "CircleErrorEmailNotVerified",
        ["invitation_rate_limit_reached"] = "CircleErrorInvitationRateLimit",
        ["circle_creation_rate_limit_reached"] = "CircleErrorCreationRateLimit",
        ["owned_circle_limit_reached"] = "CircleErrorOwnedLimit",
        ["circle_full"] = "CircleErrorFull",
        ["pending_invitation_limit_reached"] = "CircleErrorPendingInvitationLimit",
        ["already_a_member"] = "CircleErrorAlreadyAMember",
        ["membership_not_found"] = "CircleErrorMembershipNotFound",
        ["managed_member_not_found"] = "CircleErrorManagedMemberNotFound",
        ["managed_member_cannot_be_elevated"] = "CircleErrorManagedCannotBeElevated",
        ["managed_member_cannot_leave"] = "CircleErrorManagedCannotLeave",
        ["owner_cannot_be_removed"] = "CircleErrorOwnerCannotBeRemoved",
        ["owner_cannot_be_demoted"] = "CircleErrorOwnerCannotBeDemoted",
        ["ownership_requires_transfer"] = "CircleErrorOwnershipRequiresTransfer",
        ["admin_cannot_remove_admin"] = "CircleErrorAdminCannotRemoveAdmin",
        ["cannot_remove_yourself"] = "CircleErrorCannotRemoveYourself",
        ["transfer_target_is_managed"] = "CircleErrorTransferTargetIsManaged",
        ["already_the_owner"] = "CircleErrorAlreadyTheOwner",
        ["owner_must_transfer_ownership"] = "CircleErrorOwnerMustTransfer",
        ["owner_must_delete_circle"] = "CircleErrorOwnerMustDelete",
        ["circles_still_owned"] = "CircleErrorCirclesStillOwned",
        ["refused"] = "CircleErrorRefused",
    };

    /// <summary>Every refusal code this mapping covers, for the test that pins it to the API.</summary>
    public static IReadOnlyCollection<string> KnownCodes => _codes.Keys;

    /// <summary>Describes a circle failure.</summary>
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

        return error.Code is { } code && _codes.TryGetValue(code, out var key)
            ? localizer[key]
            : ApiErrorText.Describe(localizer, error);
    }
}
