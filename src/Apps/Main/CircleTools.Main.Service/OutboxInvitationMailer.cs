// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using CircleTools.Circles.Core.Domain;
using CircleTools.Infrastructure.Mail;
using CircleTools.Infrastructure.Outbox;

namespace CircleTools.Main.Service;

/// <summary>
/// Implements the Circles context's <see cref="IInvitationMailer"/> port over the existing outbox
/// (FR-017).
/// </summary>
/// <param name="outbox">The durable outbox.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
/// <para>
/// This adapter lives in the API host rather than in <c>Circles.Core</c> on purpose: the port names
/// what the domain needs — "get this invitation on its way" — while the outbox, the SMTP client and
/// the template are host concerns. Referencing <c>CircleTools.Infrastructure</c> from the domain project
/// would have pulled MailKit into it for one call.
/// </para>
/// <para>
/// It <strong>enqueues and returns</strong>, which is the port's stated contract and a security
/// control, not an optimisation: sending inline would make inviting an address that has an account
/// measurably slower than inviting one that does not, and that difference is the signal an attacker
/// measures (NFR-005). It is also why an invitation whose delivery later fails leaves the invitation
/// pending and tells the inviter nothing.
/// </para>
/// <para>
/// The queued payload carries the link <em>secret</em>, because the mail cannot be rendered without
/// it and the circle keeps only its handle. The row is therefore as sensitive as the mail it will
/// become, exactly like the verification and reset payloads M1 already enqueues.
/// </para>
/// </remarks>
public sealed class OutboxInvitationMailer(IOutbox outbox, TimeProvider clock) : IInvitationMailer
{
    /// <inheritdoc />
    public async ValueTask QueueAsync(InvitationMail mail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var payload = JsonSerializer.Serialize(new
        {
            email = mail.Email,

            // Null means "neither the recipient nor the inviter has a preference on record"; the
            // renderer resolves that to the product default. The domain does not decide which
            // language that is.
            language = mail.Language,
            circleName = mail.CircleName,
            circleLabel = mail.CircleLabel,
            inviterDisplayName = mail.InviterDisplayName,
            secret = mail.Secret,
            hasAccount = mail.RecipientHasAccount,
        });

        await outbox.EnqueueAsync(
            OutboxMessage.Create(MailTemplateRenderer.InvitationType, payload, clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }
}
