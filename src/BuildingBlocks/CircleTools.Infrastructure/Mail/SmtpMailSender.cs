// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CircleTools.Infrastructure.Mail;

/// <summary>
/// Sends mail over SMTP — the development adapter behind <see cref="IMailSender"/> (ADR-0023).
/// </summary>
/// <param name="options">SMTP host, port and sender identity.</param>
/// <remarks>
/// Points at the Mailpit container in development. A hosted provider replaces this class without
/// touching any caller. Note that this proves the code <em>sends</em>; it proves nothing about
/// deliverability (SPF/DKIM/DMARC, bounces), which only a real provider exercises.
/// </remarks>
public sealed class SmtpMailSender(IOptions<MailOptions> options) : IMailSender
{
    /// <inheritdoc />
    public async Task SendAsync(OutboundMail mail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var settings = options.Value;

        // multipart/alternative when an HTML body is present, plaintext-only otherwise: text clients
        // and spam filters still get a clean plaintext part, HTML clients get the branded one (ADR-0064).
        var builder = new BodyBuilder { TextBody = mail.Body };

        if (!string.IsNullOrEmpty(mail.HtmlBody))
        {
            builder.HtmlBody = mail.HtmlBody;
        }

        var message = new MimeMessage
        {
            Subject = mail.Subject,
            Body = builder.ToMessageBody(),
        };

        // The sender display name is co-branded per source app (e.g. "Secret Santa · CircleTools");
        // the from address stays the single configured mailbox so SPF/DKIM alignment is unaffected.
        message.From.Add(new MailboxAddress(mail.FromName ?? settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.To));

        using var client = new SmtpClient();

        // The development sink speaks plain SMTP on a loopback port with no auth; a hosted provider
        // (Brevo — ADR-0037) requires STARTTLS and credentials. SecureSocketOptions.Auto upgrades to
        // STARTTLS when the server offers it, and authentication happens only when a username is
        // configured — so the same adapter serves Mailpit (no credentials) and Brevo (credentials
        // from a secret) with configuration alone, exactly as the class remark promises.
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.Auto, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }
}
