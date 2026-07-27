using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AppSuite.Infrastructure.Mail;

/// <summary>
/// Renders the M1 transactional mails in DE / EN / FR (NFR-003).
/// </summary>
/// <param name="options">Mail settings, for the link base URL.</param>
/// <remarks>
/// <para>
/// Mail always renders in the <em>account's</em> preferred language, taken from the queued
/// payload — never the requesting browser's. The person who asked for a password reset may not be
/// the account owner (UC-MAIN-007).
/// </para>
/// <para>
/// <strong>Open point:</strong> [ADR-0011] chose RESX for product strings. These templates are
/// multi-line bodies with placeholders, which RESX handles awkwardly, so they live here for now
/// behind <see cref="IMailTemplateRenderer"/>. Whether email templates follow ADR-0011 or get their
/// own mechanism is a decision still to be made — the seam keeps either option open.
/// </para>
/// </remarks>
public sealed class MailTemplateRenderer(IOptions<MailOptions> options) : IMailTemplateRenderer
{
    /// <summary>Outbox type for the sign-up verification mail (FR-007).</summary>
    public const string VerificationType = "mail.verification";

    /// <summary>Outbox type for the "someone tried to sign up" notice (FR-007, NFR-005).</summary>
    public const string SignUpAttemptType = "mail.signup-attempt";

    /// <summary>Outbox type for the password-reset mail (FR-009).</summary>
    public const string PasswordResetType = "mail.password-reset";

    /// <summary>Outbox type for the confirmation sent to a new address (FR-013).</summary>
    public const string EmailChangeType = "mail.email-change";

    /// <summary>Outbox type for the notice sent to the address being replaced (FR-013).</summary>
    public const string EmailChangeNoticeType = "mail.email-change-notice";

    private static readonly string[] SupportedLanguages = ["de", "fr", "en"];

    /// <inheritdoc />
    public OutboundMail? Render(string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var root = JsonDocument.Parse(payload).RootElement;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var language = Normalize(root.TryGetProperty("language", out var l) ? l.GetString() : null);

        return type switch
        {
            VerificationType => RenderVerification(root, email, language),
            SignUpAttemptType => RenderSignUpAttempt(email, language),
            PasswordResetType => RenderPasswordReset(root, email, language),
            EmailChangeType => RenderEmailChange(root, email, language),
            EmailChangeNoticeType => RenderEmailChangeNotice(email, language),
            _ => null,
        };
    }

    /// <summary>Reduces a culture name such as <c>de-CH</c> to a supported language.</summary>
    /// <param name="language">The account's preferred culture name.</param>
    /// <returns>One of <c>de</c>, <c>fr</c>, <c>en</c>.</returns>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "de"; // de-CH is the product default (ADR-0011).
        }

        var prefix = language.Split('-')[0].ToLowerInvariant();
        return SupportedLanguages.Contains(prefix, StringComparer.Ordinal) ? prefix : "en";
    }

    private OutboundMail RenderVerification(JsonElement root, string email, string language)
    {
        var userId = root.GetProperty("userId").GetString();
        var token = root.GetProperty("token").GetString();
        var link = string.Create(
            CultureInfo.InvariantCulture,
            $"{options.Value.ClientBaseUrl.TrimEnd('/')}/verify-email?userId={userId}&token={token}");

        var (subject, body) = language switch
        {
            "de" => (
                "Bestätigen Sie Ihre E-Mail-Adresse",
                $"Willkommen bei AppSuite.\n\nBitte bestätigen Sie Ihre E-Mail-Adresse:\n{link}\n\n" +
                "Wenn Sie kein Konto erstellt haben, können Sie diese E-Mail ignorieren."),
            "fr" => (
                "Confirmez votre adresse e-mail",
                $"Bienvenue chez AppSuite.\n\nVeuillez confirmer votre adresse e-mail :\n{link}\n\n" +
                "Si vous n'avez pas créé de compte, vous pouvez ignorer cet e-mail."),
            _ => (
                "Confirm your email address",
                $"Welcome to AppSuite.\n\nPlease confirm your email address:\n{link}\n\n" +
                "If you did not create an account, you can ignore this email."),
        };

        return new OutboundMail(email, subject, body);
    }

    private OutboundMail RenderPasswordReset(JsonElement root, string email, string language)
    {
        var userId = root.GetProperty("userId").GetString();
        var token = root.GetProperty("token").GetString();
        var link = string.Create(
            CultureInfo.InvariantCulture,
            $"{options.Value.ClientBaseUrl.TrimEnd('/')}/reset-password?userId={userId}&token={token}");

        var (subject, body) = language switch
        {
            "de" => (
                "Passwort zurücksetzen",
                $"Sie haben angefordert, Ihr AppSuite-Passwort zurückzusetzen.\n\n{link}\n\n" +
                "Der Link ist nur einmal und nur kurze Zeit gültig. Ihr aktuelles Passwort bleibt " +
                "gültig, bis Sie ein neues setzen. Falls Sie das nicht angefordert haben, ist " +
                "nichts passiert."),
            "fr" => (
                "Réinitialiser votre mot de passe",
                $"Vous avez demandé la réinitialisation de votre mot de passe AppSuite.\n\n{link}\n\n" +
                "Ce lien est à usage unique et expire rapidement. Votre mot de passe actuel reste " +
                "valide jusqu'à ce que vous en définissiez un nouveau. Si vous n'êtes pas à " +
                "l'origine de cette demande, rien ne s'est passé."),
            _ => (
                "Reset your password",
                $"You asked to reset your AppSuite password.\n\n{link}\n\n" +
                "The link is single-use and expires shortly. Your current password keeps working " +
                "until you set a new one. If you didn't ask for this, nothing has happened."),
        };

        return new OutboundMail(email, subject, body);
    }

    private OutboundMail RenderEmailChange(JsonElement root, string email, string language)
    {
        var userId = root.GetProperty("userId").GetString();
        var token = root.GetProperty("token").GetString();
        var link = string.Create(
            CultureInfo.InvariantCulture,
            $"{options.Value.ClientBaseUrl.TrimEnd('/')}/confirm-email-change?userId={userId}" +
            $"&newEmail={Uri.EscapeDataString(email)}&token={token}");

        var (subject, body) = language switch
        {
            "de" => (
                "Neue E-Mail-Adresse bestätigen",
                $"Bestätigen Sie diese Adresse, um sie für Ihr AppSuite-Konto zu verwenden:\n{link}\n\n" +
                "Bis zur Bestätigung bleibt Ihre bisherige Adresse aktiv."),
            "fr" => (
                "Confirmez votre nouvelle adresse e-mail",
                $"Confirmez cette adresse pour l'utiliser avec votre compte AppSuite :\n{link}\n\n" +
                "Votre adresse actuelle reste active jusqu'à la confirmation."),
            _ => (
                "Confirm your new email address",
                $"Confirm this address to use it for your AppSuite account:\n{link}\n\n" +
                "Your current address stays active until you confirm."),
        };

        return new OutboundMail(email, subject, body);
    }

    private static OutboundMail RenderEmailChangeNotice(string email, string language)
    {
        // Goes to the address being replaced, so the real owner hears about a change while they
        // still control the account (FR-013).
        var (subject, body) = language switch
        {
            "de" => (
                "Änderung Ihrer E-Mail-Adresse angefordert",
                "Für Ihr AppSuite-Konto wurde eine neue E-Mail-Adresse angefordert.\n\n" +
                "Ihre bisherige Adresse bleibt aktiv, bis die neue bestätigt wird. Falls Sie das " +
                "nicht waren, ändern Sie bitte umgehend Ihr Passwort."),
            "fr" => (
                "Changement d'adresse e-mail demandé",
                "Une nouvelle adresse e-mail a été demandée pour votre compte AppSuite.\n\n" +
                "Votre adresse actuelle reste active jusqu'à la confirmation de la nouvelle. Si " +
                "vous n'êtes pas à l'origine de cette demande, changez votre mot de passe " +
                "immédiatement."),
            _ => (
                "Email address change requested",
                "A new email address was requested for your AppSuite account.\n\n" +
                "Your current address stays active until the new one is confirmed. If this " +
                "wasn't you, change your password immediately."),
        };

        return new OutboundMail(email, subject, body);
    }

    private static OutboundMail RenderSignUpAttempt(string email, string language)
    {
        // Sent to the *existing* account holder when someone tries to register their address. It
        // must never appear in the response to the person who tried (NFR-005), and it must not
        // confirm anything about the attempt beyond "it happened".
        var (subject, body) = language switch
        {
            "de" => (
                "Anmeldeversuch mit Ihrer E-Mail-Adresse",
                "Jemand hat versucht, ein AppSuite-Konto mit Ihrer E-Mail-Adresse zu erstellen.\n\n" +
                "Sie haben bereits ein Konto — melden Sie sich stattdessen an. Falls Sie das nicht " +
                "waren, ist nichts passiert und Sie müssen nichts unternehmen."),
            "fr" => (
                "Tentative d'inscription avec votre adresse e-mail",
                "Quelqu'un a tenté de créer un compte AppSuite avec votre adresse e-mail.\n\n" +
                "Vous avez déjà un compte — connectez-vous plutôt. Si ce n'était pas vous, rien " +
                "ne s'est passé et vous n'avez rien à faire."),
            _ => (
                "Sign-up attempt with your email address",
                "Someone tried to create an AppSuite account with your email address.\n\n" +
                "You already have an account — sign in instead. If this wasn't you, nothing " +
                "happened and there is nothing you need to do."),
        };

        return new OutboundMail(email, subject, body);
    }
}
