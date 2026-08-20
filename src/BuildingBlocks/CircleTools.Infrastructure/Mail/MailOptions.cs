namespace CircleTools.Infrastructure.Mail;

/// <summary>Outbound mail settings (ADR-0023).</summary>
public sealed class MailOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Mail";

    /// <summary>Gets or sets the SMTP host.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the SMTP port.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>
    /// Gets or sets the SMTP username. Empty for the development sink (Mailpit, no auth); set from a
    /// secret for a hosted provider (Brevo — ADR-0037). When empty the sender connects without
    /// authenticating, preserving the loopback dev behaviour.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP password / API key. Supplied from a secret, never committed.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the address mail is sent from.</summary>
    public string FromAddress { get; set; } = "no-reply@circletools.test";

    /// <summary>Gets or sets the display name shown as the sender.</summary>
    public string FromName { get; set; } = "CircleTools";

    /// <summary>
    /// Gets or sets the base URL links in mail point at — the web client's origin.
    /// </summary>
    public string ClientBaseUrl { get; set; } = "https://localhost:7080";
}
