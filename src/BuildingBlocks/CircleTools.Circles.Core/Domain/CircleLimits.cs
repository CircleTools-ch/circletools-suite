// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// The caps and lifetimes FEAT-003 fixed for circles. Every one of them is an <em>abuse</em> limit
/// rather than a product limit, and every one is a guess until real use says otherwise — which is
/// why they are configuration with documented defaults rather than constants in the domain.
/// </summary>
public sealed class CircleLimits
{
    /// <summary>The configuration section these limits are bound from.</summary>
    public const string SectionName = "Circles:Limits";

    /// <summary>
    /// Gets or sets how many people one circle may hold, managed members included. Default 100:
    /// an extended family or a school class is well under it, and unbounded rows are the real risk.
    /// </summary>
    public int PeoplePerCircle { get; set; } = 100;

    /// <summary>
    /// Gets or sets how many invitations one circle may have outstanding. Default 50 — a circle
    /// capped at 100 people cannot legitimately owe more than half of them an answer at once.
    /// </summary>
    public int PendingInvitationsPerCircle { get; set; } = 50;

    /// <summary>
    /// Gets or sets how many circles one user may own. Default 20. Joined circles are unlimited:
    /// you cannot be spammed into a circle you did not accept, so only creation needs a bound.
    /// </summary>
    public int OwnedCirclesPerUser { get; set; } = 20;

    /// <summary>Gets or sets how many circles one user may create per day. Default 10.</summary>
    public int CircleCreationsPerDay { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many invitations one inviter may send per hour, across all circles.
    /// Default 20 — above any plausible burst, far below what a mail relay needs.
    /// </summary>
    public int InvitationsPerInviterPerHour { get; set; } = 20;

    /// <summary>Gets or sets how many invitations one inviter may send per day. Default 60.</summary>
    public int InvitationsPerInviterPerDay { get; set; } = 60;

    /// <summary>
    /// Gets or sets how many invitations one <em>address</em> may receive per day, whatever the
    /// sender or circle. Default 3. This is the only limit that protects the third party: keyed on
    /// the inviter it would be bypassed by inviting from three circles.
    /// </summary>
    public int InvitationsPerRecipientPerDay { get; set; } = 3;

    /// <summary>
    /// Gets or sets how long an invitation stays acceptable. Default 14 days — long enough to
    /// survive a holiday week, short enough not to sit live in someone else's inbox for a year.
    /// </summary>
    public TimeSpan InvitationLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Gets or sets how long a declined address is silently un-invitable. Default 7 days. It is
    /// enforced <em>without telling the inviter</em>: a cooldown they could observe would prove the
    /// invitation was declined, which is what makes a decline indistinguishable from an expiry.
    /// </summary>
    public TimeSpan DeclineCooldown { get; set; } = TimeSpan.FromDays(7);
}
