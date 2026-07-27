using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace AppSuite.Infrastructure.Mail;

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
        var message = new MimeMessage
        {
            Subject = mail.Subject,
            Body = new TextPart(TextFormat.Plain) { Text = mail.Body },
        };

        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.To));

        using var client = new SmtpClient();

        // The development sink speaks plain SMTP on a loopback port. A real provider will require
        // STARTTLS and credentials — configured, not hard-coded, when that decision lands.
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.Auto, cancellationToken)
            .ConfigureAwait(false);
        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }
}
