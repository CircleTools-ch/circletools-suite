// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Infrastructure.Mail;

/// <summary>A rendered message ready to send.</summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line, in the recipient's language.</param>
/// <param name="Body">Plain-text body, in the recipient's language — the fallback for text-only clients.</param>
/// <param name="HtmlBody">
/// Branded HTML body, in the recipient's language (ADR-0064). When present the sender ships a
/// <c>multipart/alternative</c> with both parts; when <see langword="null"/> the mail is plaintext only.
/// </param>
/// <param name="FromName">
/// The sender display name for this message (ADR-0064). Co-branded per source app (e.g.
/// "Secret Santa · CircleTools"); when <see langword="null"/> the sender falls back to the configured
/// <see cref="MailOptions.FromName"/>.
/// </param>
public sealed record OutboundMail(string To, string Subject, string Body, string? HtmlBody = null, string? FromName = null);

/// <summary>Sends already-rendered mail.</summary>
/// <remarks>
/// The provider behind this is deliberately undecided ([ADR-0023]). Only the SMTP adapter exists
/// today, pointed at a development sink; swapping in a hosted provider is an adapter plus
/// configuration rather than a change to any caller.
/// </remarks>
public interface IMailSender
{
    /// <summary>Sends one message.</summary>
    /// <param name="mail">The rendered message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the message is handed to the transport.</returns>
    Task SendAsync(OutboundMail mail, CancellationToken cancellationToken = default);
}

/// <summary>Turns a queued outbox message into a rendered, localized mail.</summary>
public interface IMailTemplateRenderer
{
    /// <summary>Renders a message, or returns <c>null</c> if its type is not a mail.</summary>
    /// <param name="type">The outbox message type.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <returns>The rendered mail, or <c>null</c>.</returns>
    OutboundMail? Render(string type, string payload);
}
