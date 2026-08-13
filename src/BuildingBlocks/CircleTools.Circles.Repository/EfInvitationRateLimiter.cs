using CircleTools.Circles.Core.Domain;
using CircleTools.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Circles.Repository;

/// <summary>
/// The persisted implementation of FR-017's three invitation limits.
/// </summary>
/// <param name="context">The circles database context.</param>
/// <param name="clock">The time source, injected so the windows are testable without waiting.</param>
/// <remarks>
/// <para>
/// These are not the per-IP limiter the API already has, and they are not a substitute for it. The
/// things protected here are an inviter, a recipient and a circle, none of which is an IP address —
/// and the per-recipient limit is the only one that bounds what one person can inflict on another,
/// because keyed on the inviter it would be bypassed by inviting from three circles.
/// </para>
/// <para>
/// The windows are rolling. A fixed window that resets on the hour lets twice the limit through
/// across the boundary, which for a mail relay is the only boundary that matters.
/// </para>
/// </remarks>
public sealed class EfInvitationRateLimiter(CirclesDbContext context, TimeProvider clock) : IInvitationRateLimiter
{
    /// <inheritdoc />
    public async Task<InvitationRateLimitDecision> TryConsumeAsync(
        Guid inviterUserId,
        string recipientEmailHash,
        CircleLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmailHash);

        var now = clock.GetUtcNow();
        var hourAgo = now - TimeSpan.FromHours(1);
        var dayAgo = now - TimeSpan.FromDays(1);

        var byInviter = await context.InvitationAttempts
            .Where(a => a.InviterUserId == inviterUserId && a.AttemptedAt >= dayAgo)
            .Select(a => a.AttemptedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byRecipient = await context.InvitationAttempts
            .Where(a => a.RecipientEmailHash == recipientEmailHash && a.AttemptedAt >= dayAgo)
            .Select(a => a.AttemptedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset? retryAfter = null;

        Breach(byInviter.Where(t => t >= hourAgo), limits.InvitationsPerInviterPerHour, TimeSpan.FromHours(1));
        Breach(byInviter, limits.InvitationsPerInviterPerDay, TimeSpan.FromDays(1));
        Breach(byRecipient, limits.InvitationsPerRecipientPerDay, TimeSpan.FromDays(1));

        if (retryAfter is { } when)
        {
            // Refused attempts are deliberately not recorded. Charging them would keep the window
            // permanently full for anyone retrying, so a limit hit once would never lift.
            return InvitationRateLimitDecision.Refused(when);
        }

        // Recorded before the invitation is decided, and recorded whatever the outcome — including
        // the attempts that silently send nothing because the address is already in the circle or
        // declined recently. Charging only the attempts that produce a mail would make the counter
        // differ between those two worlds, and an inviter who hit the limit sooner than expected
        // would have learned something about the address (NFR-005).
        context.InvitationAttempts.Add(new InvitationAttempt
        {
            Id = GuidV7.New(),
            InviterUserId = inviterUserId,
            RecipientEmailHash = recipientEmailHash,
            AttemptedAt = now,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return InvitationRateLimitDecision.Allowed;

        // A breached limit frees a slot when its oldest attempt falls out of the window. With more
        // than one breached, the caller waits for the latest of those moments — the refusal says
        // when they may continue rather than only that they may not (FR-017).
        void Breach(IEnumerable<DateTimeOffset> window, int limit, TimeSpan length)
        {
            var attempts = window.ToList();
            if (attempts.Count < limit)
            {
                return;
            }

            var frees = attempts.Min() + length;
            if (retryAfter is null || frees > retryAfter)
            {
                retryAfter = frees;
            }
        }
    }
}
