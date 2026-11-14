// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace CircleTools.Infrastructure.Mail;

/// <summary>The resolved mail identity for one message — the source app's branding (ADR-0064).</summary>
/// <param name="Name">Display name shown in the header (e.g. "Secret Santa").</param>
/// <param name="BaseUrl">Origin the links point at, trimmed of any trailing slash.</param>
/// <param name="BrandColor">Hex colour for the header band, CTA button and heading.</param>
/// <param name="LogoUrl">Optional absolute URL of a header logo image; wordmark-only when null.</param>
/// <param name="SenderName">The inbox sender display name.</param>
internal sealed record MailBranding(string Name, string BaseUrl, string BrandColor, string? LogoUrl, string SenderName);

/// <summary>The language-independent content of one mail, ready to lay out (ADR-0064).</summary>
/// <param name="Subject">The subject line.</param>
/// <param name="Heading">The heading shown at the top of the body.</param>
/// <param name="Paragraphs">Body paragraphs, in order.</param>
/// <param name="CtaLabel">The call-to-action button label, or null for a link-less mail.</param>
/// <param name="Link">The action URL behind the button and the fallback line, or null.</param>
/// <param name="FooterNote">The "why you received this" line shown muted in the footer.</param>
internal sealed record MailModel(
    string Subject,
    string Heading,
    IReadOnlyList<string> Paragraphs,
    string? CtaLabel,
    string? Link,
    string FooterNote);

/// <summary>
/// Renders a <see cref="MailModel"/> into a branded, table-based HTML body and a plaintext fallback
/// (ADR-0064). Table layout + inline CSS is the only markup email clients render reliably; all dynamic
/// text is HTML-encoded so user-supplied names cannot inject markup.
/// </summary>
internal static class MailLayout
{
    /// <summary>Renders both bodies for one message.</summary>
    /// <param name="brand">The resolved source-app branding.</param>
    /// <param name="language">One of <c>de</c>, <c>fr</c>, <c>en</c>.</param>
    /// <param name="model">The message content.</param>
    /// <returns>The HTML body and the plaintext body.</returns>
    public static (string Html, string Text) Render(MailBranding brand, string language, MailModel model)
    {
        var chrome = Chrome.For(language);
        return (BuildHtml(brand, chrome, model), BuildText(chrome, model));
    }

    private static string BuildHtml(MailBranding brand, Chrome chrome, MailModel model)
    {
        var color = brand.BrandColor;
        var baseUrl = brand.BaseUrl;
        var sb = new StringBuilder(2560);

        sb.Append("<!doctype html><html lang=\"").Append(Enc(chrome.Lang)).Append("\"><head>")
          .Append("<meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<title>").Append(Enc(model.Subject)).Append("</title>")
          // Fluid by default (below); this only tightens the gutters on a phone, where clients that
          // support media queries honour it and the rest keep the inline padding.
          .Append("<style>@media only screen and (max-width:480px){")
          .Append(".ct-shell{padding:12px 8px !important}")
          .Append(".ct-pad{padding-left:20px !important;padding-right:20px !important}")
          .Append("}</style></head>")
          .Append("<body style=\"margin:0;padding:0;background:#eef0f3;\">");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#eef0f3;\">")
          .Append("<tr><td align=\"center\" class=\"ct-shell\" style=\"padding:24px 12px;\">")
          // Fluid width capped at 600px, so the card shrinks to fit a phone instead of forcing sideways scroll.
          .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ")
          .Append("style=\"width:100%;max-width:600px;background:#ffffff;border-radius:14px;overflow:hidden;")
          .Append("font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;\">");

        // Header band — brand colour, optional logo image, white wordmark.
        sb.Append("<tr><td class=\"ct-pad\" style=\"background:").Append(color).Append(";padding:24px 32px;\">");
        if (!string.IsNullOrEmpty(brand.LogoUrl))
        {
            sb.Append("<img src=\"").Append(Enc(brand.LogoUrl)).Append("\" width=\"40\" height=\"40\" alt=\"")
              .Append(Enc(brand.Name)).Append("\" style=\"vertical-align:middle;border:0;margin-right:10px;\">");
        }

        sb.Append("<span style=\"color:#ffffff;font-size:20px;font-weight:700;vertical-align:middle;\">")
          .Append(Enc(brand.Name)).Append("</span></td></tr>");

        // Body — heading, paragraphs, CTA button + fallback link.
        sb.Append("<tr><td class=\"ct-pad\" style=\"padding:32px;\">")
          .Append("<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.3;color:").Append(color).Append(";\">")
          .Append(Enc(model.Heading)).Append("</h1>");

        foreach (var paragraph in model.Paragraphs)
        {
            sb.Append("<p style=\"margin:0 0 16px;font-size:16px;line-height:1.6;color:#333a48;\">")
              .Append(Enc(paragraph)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(model.CtaLabel) && !string.IsNullOrEmpty(model.Link))
        {
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:24px 0 8px;\">")
              .Append("<tr><td style=\"border-radius:10px;background:").Append(color).Append(";\">")
              .Append("<a href=\"").Append(Enc(model.Link)).Append("\" style=\"display:inline-block;padding:14px 30px;")
              .Append("font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:10px;\">")
              .Append(Enc(model.CtaLabel)).Append("</a></td></tr></table>");

            sb.Append("<p style=\"margin:24px 0 6px;font-size:13px;color:#6b7280;\">")
              .Append(Enc(chrome.Fallback)).Append("</p>")
              .Append("<p style=\"margin:0;font-size:13px;word-break:break-all;\"><a href=\"")
              .Append(Enc(model.Link)).Append("\" style=\"color:#3c6fd1;\">")
              .Append(Enc(model.Link)).Append("</a></p>");
        }

        sb.Append("</td></tr>");

        // Footer — why-line, legal links, account/org line.
        sb.Append("<tr><td class=\"ct-pad\" style=\"background:#f6f7f9;padding:20px 32px 24px;border-top:1px solid #eceef2;\">")
          .Append("<p style=\"margin:0 0 12px;font-size:13px;line-height:1.5;color:#8b90a0;\">")
          .Append(Enc(model.FooterNote)).Append("</p>")
          .Append("<p style=\"margin:0 0 10px;font-size:13px;\">")
          .Append(FooterLink(baseUrl, "/privacy", chrome.Privacy)).Append(" &nbsp;·&nbsp; ")
          .Append(FooterLink(baseUrl, "/imprint", chrome.Imprint)).Append(" &nbsp;·&nbsp; ")
          .Append(FooterLink(baseUrl, "/delete-account", chrome.Delete)).Append("</p>")
          .Append("<p style=\"margin:0;font-size:12px;color:#a7acba;\">")
          .Append(Enc(chrome.AccountLine)).Append(" &nbsp;·&nbsp; CircleTools · circletools.ch</p>")
          .Append("</td></tr>");

        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    private static string BuildText(Chrome chrome, MailModel model)
    {
        var sb = new StringBuilder(512);
        sb.Append(model.Heading).Append("\n\n");

        foreach (var paragraph in model.Paragraphs)
        {
            sb.Append(paragraph).Append("\n\n");
        }

        if (!string.IsNullOrEmpty(model.CtaLabel) && !string.IsNullOrEmpty(model.Link))
        {
            sb.Append(model.CtaLabel).Append(":\n").Append(model.Link).Append("\n\n");
        }

        sb.Append(model.FooterNote).Append("\n\n")
          .Append("— ").Append(chrome.AccountLine).Append("\nCircleTools · circletools.ch");
        return sb.ToString();
    }

    private static string FooterLink(string baseUrl, string path, string label) =>
        $"<a href=\"{Enc(baseUrl + path)}\" style=\"color:#3c6fd1;text-decoration:none;\">{Enc(label)}</a>";

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>Localized footer/chrome strings — the parts of the layout that are not per-message.</summary>
    private sealed record Chrome(
        string Lang,
        string Fallback,
        string Privacy,
        string Imprint,
        string Delete,
        string AccountLine)
    {
        public static Chrome For(string language) => language switch
        {
            "de" => new(
                "de",
                "Der Button funktioniert nicht? Kopiere diesen Link in deinen Browser:",
                "Datenschutz",
                "Über & Rechtliches",
                "Daten löschen",
                "Gesendet für dein CircleTools-Konto."),
            "fr" => new(
                "fr",
                "Le bouton ne fonctionne pas ? Copiez ce lien dans votre navigateur :",
                "Confidentialité",
                "Mentions légales",
                "Supprimer mes données",
                "Envoyé pour votre compte CircleTools."),
            _ => new(
                "en",
                "Button not working? Copy and paste this link into your browser:",
                "Privacy",
                "About & legal",
                "Delete your data",
                "Sent for your CircleTools account."),
        };
    }
}
