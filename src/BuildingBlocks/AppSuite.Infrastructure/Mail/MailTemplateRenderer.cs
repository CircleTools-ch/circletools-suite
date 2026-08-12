using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AppSuite.Infrastructure.Mail;

/// <summary>
/// Renders the suite's transactional mails in DE / EN / FR (NFR-003).
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

    /// <summary>Outbox type for the circle invitation (FR-017).</summary>
    /// <remarks>
    /// The first mail the suite sends to someone who has no relationship with it. Everything it may
    /// say about the circle is fixed by UC-MAIN-010: its name, its label and the inviter's display
    /// name — never who else is in it, which is member-only data (NFR-006).
    /// </remarks>
    public const string InvitationType = "mail.circle-invitation";

    /// <summary>Outbox type for the member-link request (FR-028, M4 — Linking).</summary>
    /// <remarks>
    /// The Secret-Santa variant of the invitation: it asks the recipient to claim a member of a group
    /// so they own their wishlist and see their own assignment. Like the circle invitation it says only
    /// the group's name, the member's display name and the inviter — never the roster or the draw.
    /// </remarks>
    public const string MemberLinkType = "mail.member-link";

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
            InvitationType => RenderInvitation(root, email, language),
            MemberLinkType => RenderMemberLink(root, email, language),
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
                "Bestätige deine E-Mail-Adresse",
                $"Willkommen bei AppSuite.\n\nBitte bestätige deine E-Mail-Adresse:\n{link}\n\n" +
                "Wenn du kein Konto erstellt hast, kannst du diese E-Mail ignorieren."),
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
                $"Du hast angefordert, dein AppSuite-Passwort zurückzusetzen.\n\n{link}\n\n" +
                "Der Link ist nur einmal und nur kurze Zeit gültig. Dein aktuelles Passwort bleibt " +
                "gültig, bis du ein neues setzt. Falls du das nicht angefordert hast, ist " +
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
                $"Bestätige diese Adresse, um sie für dein AppSuite-Konto zu verwenden:\n{link}\n\n" +
                "Bis zur Bestätigung bleibt deine bisherige Adresse aktiv."),
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

    private OutboundMail RenderInvitation(JsonElement root, string email, string language)
    {
        // The label is the word this circle calls itself, chosen by a user and stored as user data.
        // It is rendered exactly as it was written and is never looked up or re-translated, whatever
        // language the mail is in (FR-016).
        var circleName = root.GetProperty("circleName").GetString();
        var circleLabel = root.GetProperty("circleLabel").GetString();
        var inviter = root.GetProperty("inviterDisplayName").GetString();
        var hasAccount = root.GetProperty("hasAccount").GetBoolean();
        var baseUrl = options.Value.ClientBaseUrl.TrimEnd('/');

        // Two mails, and which one is sent is decided server-side and never surfaced through the API.
        // The recipient who already has an account is pointed at the app and carries no secret at
        // all: their join request is matched by their *verified* address, which is why that path
        // needs no token (UC-MAIN-010). The recipient without an account gets the link, and it lets
        // them read the invitation and decline it — never join.
        var link = hasAccount
            ? string.Create(CultureInfo.InvariantCulture, $"{baseUrl}/invitations")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{baseUrl}/invitation/{Uri.EscapeDataString(root.GetProperty("secret").GetString()!)}");

        var (subject, intro, action, closing) = language switch
        {
            "de" => (
                $"{inviter} lädt dich zu «{circleName}» ein",
                $"{inviter} lädt dich zu «{circleName}» ({circleLabel}) bei AppSuite ein.",
                hasAccount
                    ? $"Die Einladung wartet in der App auf dich:\n{link}"
                    : $"Hier kannst du zusagen oder absagen:\n{link}",
                "Wenn du nichts unternimmst, passiert nichts — die Einladung läuft von selbst ab."),
            "fr" => (
                $"{inviter} vous invite à «{circleName}»",
                $"{inviter} vous invite à rejoindre «{circleName}» ({circleLabel}) sur AppSuite.",
                hasAccount
                    ? $"L'invitation vous attend dans l'application :\n{link}"
                    : $"Vous pouvez accepter ou refuser ici :\n{link}",
                "Si vous ne faites rien, rien ne se passe — l'invitation expire d'elle-même."),
            _ => (
                $"{inviter} invites you to “{circleName}”",
                $"{inviter} invites you to “{circleName}” ({circleLabel}) on AppSuite.",
                hasAccount
                    ? $"The invitation is waiting for you in the app:\n{link}"
                    : $"You can accept or decline here:\n{link}",
                "If you do nothing, nothing happens — the invitation expires on its own."),
        };

        return new OutboundMail(email, subject, $"{intro}\n\n{action}\n\n{closing}");
    }

    private OutboundMail RenderMemberLink(JsonElement root, string email, string language)
    {
        // Group name and member display name are user data, rendered exactly as written and never
        // re-translated, whatever language the mail is in.
        var groupName = root.GetProperty("groupName").GetString();
        var memberDisplayName = root.GetProperty("memberDisplayName").GetString();
        var inviter = root.GetProperty("inviterDisplayName").GetString();
        var link = string.Create(CultureInfo.InvariantCulture, $"{options.Value.ClientBaseUrl.TrimEnd('/')}/my-links");

        var (subject, intro, action, closing) = language switch
        {
            "de" => (
                $"{inviter} möchte dich mit «{groupName}» verbinden",
                $"{inviter} möchte dich als «{memberDisplayName}» mit dem Wichtel-Anlass «{groupName}» " +
                "verbinden. So besitzt du deine eigene Wunschliste und siehst nur deine eigene Zuteilung.",
                $"Die Anfrage wartet in der App auf dich:\n{link}",
                "Wenn du nichts unternimmst, passiert nichts — die Anfrage läuft von selbst ab."),
            "fr" => (
                $"{inviter} souhaite vous associer à «{groupName}»",
                $"{inviter} souhaite vous associer en tant que «{memberDisplayName}» à l'événement " +
                $"«{groupName}». Vous posséderez ainsi votre propre liste de souhaits et ne verrez que " +
                "votre propre attribution.",
                $"La demande vous attend dans l'application :\n{link}",
                "Si vous ne faites rien, rien ne se passe — la demande expire d'elle-même."),
            _ => (
                $"{inviter} wants to link you to “{groupName}”",
                $"{inviter} wants to link you as “{memberDisplayName}” to the Secret-Santa event " +
                $"“{groupName}”. You'll then own your own wishlist and see only your own assignment.",
                $"The request is waiting for you in the app:\n{link}",
                "If you do nothing, nothing happens — the request expires on its own."),
        };

        return new OutboundMail(email, subject, $"{intro}\n\n{action}\n\n{closing}");
    }

    private static OutboundMail RenderEmailChangeNotice(string email, string language)
    {
        // Goes to the address being replaced, so the real owner hears about a change while they
        // still control the account (FR-013).
        var (subject, body) = language switch
        {
            "de" => (
                "Änderung deiner E-Mail-Adresse angefordert",
                "Für dein AppSuite-Konto wurde eine neue E-Mail-Adresse angefordert.\n\n" +
                "Deine bisherige Adresse bleibt aktiv, bis die neue bestätigt wird. Falls du das " +
                "nicht warst, ändere bitte umgehend dein Passwort."),
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
                "Anmeldeversuch mit deiner E-Mail-Adresse",
                "Jemand hat versucht, ein AppSuite-Konto mit deiner E-Mail-Adresse zu erstellen.\n\n" +
                "Du hast bereits ein Konto — melde dich stattdessen an. Falls du das nicht " +
                "warst, ist nichts passiert und du musst nichts unternehmen."),
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
