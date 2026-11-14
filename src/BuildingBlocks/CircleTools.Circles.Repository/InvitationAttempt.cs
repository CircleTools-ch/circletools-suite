// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Repository;

/// <summary>
/// One recorded attempt to invite an address, and the whole state behind FR-017's three
/// application-level limits: per inviter per hour, per inviter per day, and per recipient address
/// per day.
/// </summary>
/// <remarks>
/// <para>
/// This is <em>infrastructure</em>, not domain. The domain names what it needs —
/// <see cref="Core.Domain.IInvitationRateLimiter"/> — and how the count is kept is this adapter's
/// business; nothing in <c>Circles.Core</c> knows there are rows.
/// </para>
/// <para>
/// Rows rather than counters with a reset instant, because all three limits are rolling windows and
/// a fixed-window counter lets twice the limit through across a boundary. Counting rows over an
/// index is cheap at these volumes, and it is the only shape that answers "when may I continue?" —
/// the refusal has to say so (FR-017), and that answer is the oldest attempt in the window falling
/// out of it.
/// </para>
/// <para>
/// A row holds the <em>hash</em> of the recipient's address, never the address, and it is swept once
/// it is older than the longest window (NFR-004). An attempt is not evidence of anything after the
/// window it was counted in has passed.
/// </para>
/// </remarks>
public sealed class InvitationAttempt
{
    /// <summary>Gets or sets the row's identity.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the account that attempted the invitation.</summary>
    public Guid InviterUserId { get; set; }

    /// <summary>Gets or sets the hash of the invited address.</summary>
    public string RecipientEmailHash { get; set; } = string.Empty;

    /// <summary>Gets or sets when the attempt was made.</summary>
    public DateTimeOffset AttemptedAt { get; set; }
}
