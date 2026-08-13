namespace CircleTools.Circles.Core.Domain;

/// <summary>The answer to "may this invitation go out?".</summary>
/// <param name="IsAllowed">Whether the invitation may proceed.</param>
/// <param name="RetryAfter">
/// When the caller may try again, when that is known. The refusal says when they may continue
/// rather than only that they may not (FR-017).
/// </param>
public sealed record InvitationRateLimitDecision(bool IsAllowed, DateTimeOffset? RetryAfter)
{
    /// <summary>Gets the allowing decision.</summary>
    public static InvitationRateLimitDecision Allowed { get; } = new(IsAllowed: true, RetryAfter: null);

    /// <summary>Creates a refusing decision.</summary>
    /// <param name="retryAfter">When the caller may continue.</param>
    /// <returns>The refusal.</returns>
    public static InvitationRateLimitDecision Refused(DateTimeOffset retryAfter) =>
        new(IsAllowed: false, retryAfter);
}

/// <summary>
/// The three application-level invitation limits (FR-017): per inviter per hour and per day, and
/// per recipient address per day.
/// </summary>
/// <remarks>
/// These are deliberately not the per-IP limiter the API already has. The things being protected
/// here are an inviter, a recipient and a circle, and none of them is an IP address — the
/// per-recipient limit in particular is the only one that bounds what one person can inflict on
/// another, and keyed on the inviter it would be bypassed by inviting from three circles.
/// <para>
/// <strong>Both counters are consumed on every attempt</strong>, including the attempts that
/// silently send nothing because the address is already in the circle or declined recently. Charging
/// only the attempts that produce a mail would make the counters differ between those two worlds,
/// and an inviter who hit the limit sooner than expected would have learned something about the
/// address (NFR-005).
/// </para>
/// </remarks>
public interface IInvitationRateLimiter
{
    /// <summary>Records an invitation attempt and says whether it may proceed.</summary>
    /// <param name="inviterUserId">The user sending it.</param>
    /// <param name="recipientEmailHash">The hash of the invited address.</param>
    /// <param name="limits">The configured limits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decision.</returns>
    Task<InvitationRateLimitDecision> TryConsumeAsync(
        Guid inviterUserId,
        string recipientEmailHash,
        CircleLimits limits,
        CancellationToken cancellationToken = default);
}
