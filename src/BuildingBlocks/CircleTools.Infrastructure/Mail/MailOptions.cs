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
    /// Gets or sets the base URL links in mail point at — the web client's origin. Used as the
    /// fallback when no per-app origin is configured for the message's source app (ADR-0064).
    /// </summary>
    public string ClientBaseUrl { get; set; } = "https://localhost:7080";

    /// <summary>
    /// Gets or sets the app whose branding is used when a mail's source app is unknown or unset
    /// (ADR-0064). Also the identity for any future app that has not defined its own branding yet.
    /// </summary>
    public string DefaultApp { get; set; } = "circletools";

    /// <summary>
    /// Gets the per-app mail identity (name, origin, brand colour, sender name), keyed by source-app
    /// id (ADR-0064). Populated from configuration; empty falls back to <see cref="ClientBaseUrl"/>
    /// and CircleTools branding, which keeps development and tests working without this block.
    /// </summary>
    public Dictionary<string, MailApp> Apps { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Per-app mail identity — how one app's transactional mail is branded (ADR-0064).</summary>
public sealed class MailApp
{
    /// <summary>Gets or sets the app's display name in the header (e.g. "Secret Santa").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the origin links in this app's mail point at (e.g. <c>https://secretsanta.circletools.ch</c>).
    /// Empty falls back to <see cref="MailOptions.ClientBaseUrl"/>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the brand colour for the header band, CTA button and heading (hex).</summary>
    public string BrandColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender display name shown in the inbox (e.g. "Secret Santa · CircleTools").
    /// Empty falls back to <see cref="MailOptions.FromName"/>.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an absolute URL for a header logo image. Optional: email clients do not reliably
    /// render inline SVG, so this is a hosted PNG; when empty the header shows the brand-colour band
    /// with the white wordmark only (ADR-0064 follow-up).
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;
}
