// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Domain;

/// <summary>An invitation mail waiting to be queued (FR-017).</summary>
/// <param name="Email">The address to send to.</param>
/// <param name="Language">
/// The language to write in: the recipient's account language when the address has one, otherwise
/// the inviter's. The inviter never sees the mail, so choosing by recipient leaks nothing back.
/// <c>null</c> when neither has one, which the adapter reads as "the suite default" — the domain
/// does not decide which language that is.
/// </param>
/// <param name="CircleName">The circle's name.</param>
/// <param name="CircleLabel">The circle's label, in the language it was written in.</param>
/// <param name="InviterDisplayName">Who is inviting them.</param>
/// <param name="Secret">
/// The link secret. Held only long enough to render the mail — the circle keeps its handle.
/// </param>
/// <param name="RecipientHasAccount">
/// Which of the two mails to send: a sign-up link, or "open the app, it is waiting for you". This
/// is decided server-side and is never surfaced through the API, where it would answer the one
/// question the invite flow refuses to answer.
/// </param>
public sealed record InvitationMail(
    string Email,
    string? Language,
    string CircleName,
    string CircleLabel,
    string InviterDisplayName,
    string Secret,
    bool RecipientHasAccount);

/// <summary>
/// The Circles context's port for getting an invitation on its way.
/// </summary>
/// <remarks>
/// Implementations must <em>enqueue</em> and return, never send inline. Sending inline would make
/// the branch that has a mail to send measurably slower than the branch that does not, which is
/// exactly the signal an attacker measures to enumerate accounts (NFR-005) — and it is why an
/// invitation whose delivery later fails leaves the invitation pending and tells the inviter
/// nothing.
/// </remarks>
public interface IInvitationMailer
{
    /// <summary>Queues an invitation mail. Must not send it.</summary>
    /// <param name="mail">The mail to queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the mail is recorded for dispatch.</returns>
    ValueTask QueueAsync(InvitationMail mail, CancellationToken cancellationToken = default);
}
