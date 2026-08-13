using System.Globalization;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Main.Service;

/// <summary>
/// Turns a <see cref="CircleError"/> into the one HTTP answer it is allowed to give (FEAT-003).
/// </summary>
/// <remarks>
/// <para>
/// This mapping is a security control as much as a convenience. Three of its rows carry the weight:
/// </para>
/// <list type="bullet">
/// <item><see cref="CircleError.NotAMember"/> answers <strong>exactly</strong> as an unknown circle
/// does — same status, same body — because "you are not in it" and "it does not exist" must be
/// indistinguishable to anyone outside a circle (NFR-006).</item>
/// <item><see cref="CircleError.InvitationAddressMismatch"/> answers exactly as an unknown
/// invitation does. Someone who was forwarded a link and accepts with their own account must learn
/// nothing — least of all that the invited address has an account (NFR-005, FR-018).</item>
/// <item>Every refusal carries a stable <c>code</c> alongside its English title. The product is
/// DE/EN/FR and a client cannot translate prose, but it can translate <c>circle_full</c> — the same
/// reasoning that made M1 report password failures as codes (ADR-0011).</item>
/// </list>
/// </remarks>
public static class CircleProblems
{
    /// <summary>
    /// The answer given both to a circle that does not exist and to a caller who is not in it.
    /// </summary>
    /// <returns>A <c>404</c> that says nothing about which of the two it was.</returns>
    public static IResult NoSuchCircle() =>
        Problem(StatusCodes.Status404NotFound, "circle_not_found", "No such circle.");

    /// <summary>
    /// The answer given to an invitation that is unknown, revoked, consumed — or addressed to
    /// somebody else.
    /// </summary>
    /// <returns>A <c>404</c> that says nothing about which of those it was.</returns>
    public static IResult NoSuchInvitation() =>
        Problem(StatusCodes.Status404NotFound, "invitation_not_found", "This invitation is no longer valid.");

    /// <summary>Maps a refusal to its HTTP answer.</summary>
    /// <param name="error">The reason the operation was refused.</param>
    /// <param name="retryAfter">When the caller may continue, for the rate-limit refusals.</param>
    /// <returns>The response to return.</returns>
    public static IResult From(CircleError error, DateTimeOffset? retryAfter = null) => error switch
    {
        // Identical answers, deliberately.
        CircleError.NotAMember => NoSuchCircle(),
        CircleError.InvitationNotFound or CircleError.InvitationAddressMismatch => NoSuchInvitation(),

        CircleError.RoleNotPermitted => Problem(
            StatusCodes.Status403Forbidden,
            "role_not_permitted",
            "Your role in this circle does not allow that."),

        // The account is gone or its address is not verified — as far as circles are concerned those
        // are the same thing, because an unverified account gets nothing account-scoped (FR-016).
        // The code is what lets the client offer to resend the verification mail.
        CircleError.AccountNotFound => Problem(
            StatusCodes.Status403Forbidden,
            "email_not_verified",
            "Your email address must be verified first."),

        CircleError.NameRequired => Validation("name", "required"),
        CircleError.DisplayNameRequired => Validation("displayName", "required"),
        CircleError.EmailInvalid => Validation("email", "invalid"),
        CircleError.NameConfirmationMismatch => Validation("confirmedName", "mismatch"),

        // Rolling-window limits. Same body shape as the per-IP limiter's rejection, which
        // ApiResponseReader already understands.
        CircleError.InvitationRateLimitReached => TooManyRequests(
            "invitation_rate_limit_reached",
            "Too many invitations. Please try again later.",
            retryAfter),
        CircleError.CircleCreationRateLimitReached => TooManyRequests(
            "circle_creation_rate_limit_reached",
            "Too many circles created today. Please try again later.",
            retryAfter),

        // Caps. Not "try again later" — nothing frees up on its own.
        CircleError.OwnedCircleLimitReached => Conflict(
            "owned_circle_limit_reached",
            "You already own as many circles as you can."),
        CircleError.CircleFull => Conflict("circle_full", "This circle is full."),
        CircleError.PendingInvitationLimitReached => Conflict(
            "pending_invitation_limit_reached",
            "This circle has as many invitations outstanding as it can."),

        CircleError.InvitationExpired => Problem(
            StatusCodes.Status410Gone,
            "invitation_expired",
            "This invitation has expired. Ask whoever invited you for a new one."),

        CircleError.AlreadyAMember => Conflict("already_a_member", "You are already in this circle."),
        CircleError.MembershipNotFound => Problem(
            StatusCodes.Status404NotFound,
            "membership_not_found",
            "No such person in this circle."),
        CircleError.ManagedMemberNotFound => Problem(
            StatusCodes.Status404NotFound,
            "managed_member_not_found",
            "No such managed member in this circle."),

        CircleError.ManagedMemberCannotBeElevated => Conflict(
            "managed_member_cannot_be_elevated",
            "Someone without an account cannot administer a circle."),
        CircleError.ManagedMemberCannotLeave => Conflict(
            "managed_member_cannot_leave",
            "Someone without an account is removed by an owner or admin."),
        CircleError.OwnerCannotBeRemoved => Conflict(
            "owner_cannot_be_removed",
            "The owner cannot be removed from their own circle."),
        CircleError.OwnerCannotBeDemoted => Conflict(
            "owner_cannot_be_demoted",
            "The owner's role changes by transferring ownership."),
        CircleError.OwnershipRequiresTransfer => Conflict(
            "ownership_requires_transfer",
            "Ownership is handed over by transfer, not by a role change."),
        CircleError.AdminCannotRemoveAdmin => Conflict(
            "admin_cannot_remove_admin",
            "Only the owner can remove an admin."),
        CircleError.CannotRemoveYourself => Conflict(
            "cannot_remove_yourself",
            "Leave the circle instead of removing yourself."),
        CircleError.TransferTargetIsManaged => Conflict(
            "transfer_target_is_managed",
            "Ownership can only go to someone who can sign in."),
        CircleError.AlreadyTheOwner => Conflict("already_the_owner", "They already own this circle."),

        // Both of these are answers the UI acts on: one offers transfer, the other offers deletion
        // (FR-021).
        CircleError.OwnerMustTransferOwnership => Conflict(
            "owner_must_transfer_ownership",
            "Hand ownership to someone else before you leave."),
        CircleError.OwnerMustDeleteCircle => Conflict(
            "owner_must_delete_circle",
            "You are the only one who could own this circle, so leaving it means deleting it."),

        _ => Problem(StatusCodes.Status400BadRequest, "refused", "That is not allowed."),
    };

    private static IResult Problem(int statusCode, string code, string title) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static IResult Conflict(string code, string title) =>
        Problem(StatusCodes.Status409Conflict, code, title);

    private static IResult Validation(string field, string code) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [code] });

    private static RateLimitedResult TooManyRequests(string code, string message, DateTimeOffset? retryAfter) =>
        new(code, message, retryAfter);

    /// <summary>
    /// A rate-limit refusal, shaped like the per-IP limiter's so one client reader handles both:
    /// a <c>{ "message": … }</c> body and the wait in a <c>Retry-After</c> header, which is where
    /// <c>ApiResponseReader</c> looks for it.
    /// </summary>
    /// <param name="code">The stable code the client translates.</param>
    /// <param name="message">The English fallback message.</param>
    /// <param name="retryAfter">When the caller may continue, when the limiter could say.</param>
    private sealed class RateLimitedResult(string code, string message, DateTimeOffset? retryAfter) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var now = httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

            if (retryAfter > now)
            {
                var seconds = (int)Math.Ceiling((retryAfter.Value - now).TotalSeconds);
                httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            await httpContext.Response
                .WriteAsJsonAsync(new { message, code }, httpContext.RequestAborted)
                .ConfigureAwait(false);
        }
    }
}
