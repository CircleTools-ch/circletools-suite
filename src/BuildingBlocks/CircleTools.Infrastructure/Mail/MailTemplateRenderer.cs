using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CircleTools.Infrastructure.Mail;

/// <summary>
/// Renders the suite's transactional mails in DE / EN / FR (NFR-003) as branded HTML with a plaintext
/// fallback, in the identity of the source app that queued them (ADR-0064).
/// </summary>
/// <param name="options">Mail settings: per-app branding, link origins and sender identity.</param>
/// <remarks>
/// <para>
/// Mail always renders in the <em>account's</em> preferred language, taken from the queued payload —
/// never the requesting browser's. The person who asked for a password reset may not be the account
/// owner (UC-MAIN-007).
/// </para>
/// <para>
/// Each message also carries a <c>sourceApp</c> in its payload (ADR-0064): the app the user was in when
/// the mail was triggered. It selects the header colour, logo, sender name and — because the account
/// pages are mounted into every app — the origin the links return to, so a Secret-Santa sign-up gets a
/// Secret-Santa email and a Secret-Santa-looking page. Unknown or missing falls back to
/// <see cref="MailOptions.DefaultApp"/>.
/// </para>
/// <para>
/// The copy lives here (not RESX): multi-line bodies with placeholders sit awkwardly in RESX and mail
/// copy has rules UI copy does not (never re-translate user data — FR-016; no link in the enumeration
/// notices — NFR-005). This is the mechanism ADR-0064 settles. The HTML layout and plaintext are both
/// produced by <see cref="MailLayout"/> from one structured <see cref="MailModel"/>, so they cannot
/// drift.
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

    /// <summary>Outbox type for the notice that the password was changed (FR-049, ADR-0079).</summary>
    public const string PasswordChangedType = "mail.password-changed";

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

    /// <summary>Outbox type for the account-deletion confirmation ("farewell") mail (FR-047, ADR-0064).</summary>
    /// <remarks>
    /// A one-time confirmation sent after an account is deleted. It carries no link and no assignment —
    /// there is nothing to act on; it only confirms the deletion happened and thanks the person.
    /// </remarks>
    public const string AccountDeletedType = "mail.account-deleted";

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
            SignUpAttemptType => RenderSignUpAttempt(root, email, language),
            PasswordResetType => RenderPasswordReset(root, email, language),
            EmailChangeType => RenderEmailChange(root, email, language),
            EmailChangeNoticeType => RenderEmailChangeNotice(root, email, language),
            PasswordChangedType => RenderPasswordChanged(root, email, language),
            InvitationType => RenderInvitation(root, email, language),
            MemberLinkType => RenderMemberLink(root, email, language),
            AccountDeletedType => RenderAccountDeleted(root, email, language),
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
        var brand = ResolveBranding(SourceApp(root));
        var link = $"{brand.BaseUrl}/verify-email?userId={root.GetProperty("userId").GetString()}&token={root.GetProperty("token").GetString()}";

        var model = language switch
        {
            "de" => new MailModel(
                "Bestätige deine E-Mail-Adresse",
                "Bestätige deine E-Mail-Adresse",
                ["Willkommen bei CircleTools! Bitte bestätige, dass dies deine Adresse ist, damit wir dein Konto fertig einrichten können."],
                "E-Mail bestätigen",
                link,
                "Du erhältst diese E-Mail, weil mit dieser Adresse ein CircleTools-Konto erstellt wurde. Warst du das nicht? Ignoriere diese E-Mail einfach."),
            "fr" => new MailModel(
                "Confirmez votre adresse e-mail",
                "Confirmez votre adresse e-mail",
                ["Bienvenue chez CircleTools ! Confirmez qu'il s'agit bien de votre adresse pour finaliser la création de votre compte."],
                "Confirmer l'e-mail",
                link,
                "Vous recevez ceci car cette adresse a servi à créer un compte CircleTools. Ce n'était pas vous ? Ignorez cet e-mail."),
            _ => new MailModel(
                "Confirm your email address",
                "Confirm your email address",
                ["Welcome to CircleTools! Please confirm this is your address so we can finish setting up your account."],
                "Confirm email",
                link,
                "You're receiving this because someone used this address to create a CircleTools account. Didn't sign up? Just ignore this email."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderPasswordReset(JsonElement root, string email, string language)
    {
        var brand = ResolveBranding(SourceApp(root));
        var link = $"{brand.BaseUrl}/reset-password?userId={root.GetProperty("userId").GetString()}&token={root.GetProperty("token").GetString()}";

        var model = language switch
        {
            "de" => new MailModel(
                "Passwort zurücksetzen",
                "Setze dein Passwort zurück",
                ["Du hast angefordert, dein CircleTools-Passwort zurückzusetzen. Wähle mit der Schaltfläche unten ein neues – der Link ist nur einmal und nur kurze Zeit gültig. Dein aktuelles Passwort bleibt gültig, bis du ein neues setzt."],
                "Passwort zurücksetzen",
                link,
                "Wenn du das nicht angefordert hast, kannst du diese E-Mail ignorieren – dein Passwort bleibt unverändert."),
            "fr" => new MailModel(
                "Réinitialiser votre mot de passe",
                "Réinitialisez votre mot de passe",
                ["Vous avez demandé la réinitialisation de votre mot de passe CircleTools. Choisissez-en un nouveau avec le bouton ci-dessous — le lien est à usage unique et expire rapidement. Votre mot de passe actuel reste valide jusqu'à ce que vous en définissiez un nouveau."],
                "Réinitialiser le mot de passe",
                link,
                "Si vous n'êtes pas à l'origine de cette demande, ignorez cet e-mail : votre mot de passe ne changera pas."),
            _ => new MailModel(
                "Reset your password",
                "Reset your password",
                ["You asked to reset your CircleTools password. Choose a new one with the button below — the link is single-use and expires shortly. Your current password keeps working until you set a new one."],
                "Reset password",
                link,
                "If you didn't ask for this, you can safely ignore it — your password won't change."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderEmailChange(JsonElement root, string email, string language)
    {
        var brand = ResolveBranding(SourceApp(root));
        var link = $"{brand.BaseUrl}/confirm-email-change?userId={root.GetProperty("userId").GetString()}" +
            $"&newEmail={Uri.EscapeDataString(email)}&token={root.GetProperty("token").GetString()}";

        var model = language switch
        {
            "de" => new MailModel(
                "Neue E-Mail-Adresse bestätigen",
                "Bestätige deine neue E-Mail-Adresse",
                ["Bestätige diese Adresse, um sie für dein CircleTools-Konto zu verwenden. Deine bisherige Adresse bleibt aktiv, bis die neue bestätigt ist."],
                "Adresse bestätigen",
                link,
                "Du änderst die Adresse deines CircleTools-Kontos. Wenn du das nicht warst, ignoriere diese E-Mail und ändere dein Passwort."),
            "fr" => new MailModel(
                "Confirmez votre nouvelle adresse e-mail",
                "Confirmez votre nouvelle adresse e-mail",
                ["Confirmez cette adresse pour l'utiliser avec votre compte CircleTools. Votre adresse actuelle reste active jusqu'à la confirmation."],
                "Confirmer l'adresse",
                link,
                "Vous changez l'adresse de votre compte CircleTools. Si ce n'était pas vous, ignorez cet e-mail et changez votre mot de passe."),
            _ => new MailModel(
                "Confirm your new email address",
                "Confirm your new email address",
                ["Confirm this address to use it for your CircleTools account. Your current address stays active until you confirm."],
                "Confirm address",
                link,
                "You're changing the address on your CircleTools account. If this wasn't you, ignore this email and change your password."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderInvitation(JsonElement root, string email, string language)
    {
        // The label is the word this circle calls itself, chosen by a user and stored as user data. It
        // is rendered exactly as written and never looked up or re-translated, whatever language the
        // mail is in (FR-016). MailLayout HTML-encodes it, which preserves the text.
        var circleName = root.GetProperty("circleName").GetString();
        var circleLabel = root.GetProperty("circleLabel").GetString();
        var inviter = root.GetProperty("inviterDisplayName").GetString();
        var hasAccount = root.GetProperty("hasAccount").GetBoolean();
        var brand = ResolveBranding(SourceApp(root));

        // The recipient who already has an account is pointed at the app and carries no secret at all:
        // their join request is matched by their verified address (UC-MAIN-010). The recipient without
        // an account gets the link, which lets them read the invitation and decline it — never join.
        var link = hasAccount
            ? $"{brand.BaseUrl}/invitations"
            : $"{brand.BaseUrl}/invitation/{Uri.EscapeDataString(root.GetProperty("secret").GetString()!)}";

        var model = language switch
        {
            "de" => new MailModel(
                $"{inviter} lädt dich zu «{circleName}» ein",
                "Du wurdest zu einem Kreis eingeladen",
                [
                    $"{inviter} lädt dich zu «{circleName}» ({circleLabel}) bei CircleTools ein.",
                    hasAccount ? "Die Einladung wartet in der App auf dich." : "Hier kannst du sie ansehen und zu- oder absagen.",
                ],
                "Einladung ansehen",
                link,
                $"Du erhältst diese E-Mail, weil {inviter} deine Adresse für eine Einladung eingegeben hat. Sie verfällt, sobald sie angenommen, abgelehnt wird oder abläuft."),
            "fr" => new MailModel(
                $"{inviter} vous invite à «{circleName}»",
                "Vous êtes invité dans un cercle",
                [
                    $"{inviter} vous invite à rejoindre «{circleName}» ({circleLabel}) sur CircleTools.",
                    hasAccount ? "L'invitation vous attend dans l'application." : "Vous pouvez la consulter et accepter ou refuser.",
                ],
                "Voir l'invitation",
                link,
                $"Vous recevez ceci car {inviter} a saisi votre adresse pour vous inviter. Elle expire une fois acceptée, refusée ou échue."),
            _ => new MailModel(
                $"{inviter} invites you to “{circleName}”",
                "You've been invited to a circle",
                [
                    $"{inviter} invites you to “{circleName}” ({circleLabel}) on CircleTools.",
                    hasAccount ? "The invitation is waiting for you in the app." : "You can read it and accept or decline.",
                ],
                "View invitation",
                link,
                $"You received this because {inviter} entered your address to invite you. It expires once it's accepted, declined or times out."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderMemberLink(JsonElement root, string email, string language)
    {
        // Group name and member display name are user data, rendered exactly as written and never
        // re-translated, whatever language the mail is in.
        var groupName = root.GetProperty("groupName").GetString();
        var memberDisplayName = root.GetProperty("memberDisplayName").GetString();
        var inviter = root.GetProperty("inviterDisplayName").GetString();
        var brand = ResolveBranding(SourceApp(root));
        // The invitation waits in the group list, under "Invitations" (ADR-0086).
        var link = $"{brand.BaseUrl}/";

        var model = language switch
        {
            "de" => new MailModel(
                $"{inviter} lädt dich zu «{groupName}» ein",
                "Du bist zu einer Wichtel-Gruppe eingeladen",
                [
                    $"{inviter} lädt dich als «{memberDisplayName}» zur Wichtel-Gruppe «{groupName}» ein. Machst du mit, siehst du nur deine eigene Zuteilung und führst deine eigene Wunschliste.",
                    "Die Einladung wartet in der App auf dich.",
                ],
                "Einladung öffnen",
                link,
                "Du erhältst diese E-Mail, weil jemand, der die Gruppe organisiert, diese Adresse eingeladen hat. Die Einladung läuft von selbst ab, wenn du nichts tust."),
            "fr" => new MailModel(
                $"{inviter} vous invite à «{groupName}»",
                "Vous êtes invité à un groupe Père Noël secret",
                [
                    $"{inviter} vous invite en tant que «{memberDisplayName}» au groupe Père Noël secret «{groupName}». Si vous participez, vous ne voyez que votre propre attribution et tenez votre propre liste de souhaits.",
                    "L'invitation vous attend dans l'application.",
                ],
                "Ouvrir l'invitation",
                link,
                "Vous recevez ceci car une personne qui organise le groupe a invité cette adresse. L'invitation expire d'elle-même si vous ne faites rien."),
            _ => new MailModel(
                $"{inviter} invites you to “{groupName}”",
                "You're invited to a Secret Santa group",
                [
                    $"{inviter} invites you as “{memberDisplayName}” to the Secret-Santa group “{groupName}”. If you take part, you see only your own assignment and keep your own wishlist.",
                    "The invitation is waiting for you in the app.",
                ],
                "Open invitation",
                link,
                "You received this because someone organizing the group invited this address. The invitation expires on its own if you do nothing."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderEmailChangeNotice(JsonElement root, string email, string language)
    {
        // Goes to the address being replaced, so the real owner hears about a change while they still
        // control the account (FR-013). No link, by design (NFR-005).
        var brand = ResolveBranding(SourceApp(root));

        var model = language switch
        {
            "de" => new MailModel(
                "Änderung deiner E-Mail-Adresse angefordert",
                "Deine E-Mail-Adresse wird geändert",
                ["Für dein CircleTools-Konto wurde eine neue E-Mail-Adresse angefordert. Deine bisherige Adresse bleibt aktiv, bis die neue bestätigt wird. Falls du das nicht warst, ändere bitte umgehend dein Passwort."],
                null,
                null,
                "Aus Sicherheitsgründen enthält dieser Hinweis nie einen Aktionslink. Er wird an deine bisherige Adresse gesendet."),
            "fr" => new MailModel(
                "Changement d'adresse e-mail demandé",
                "Votre adresse e-mail est en cours de modification",
                ["Une nouvelle adresse e-mail a été demandée pour votre compte CircleTools. Votre adresse actuelle reste active jusqu'à la confirmation de la nouvelle. Si vous n'êtes pas à l'origine de cette demande, changez votre mot de passe immédiatement."],
                null,
                null,
                "Par sécurité, cet avis ne contient jamais de lien d'action. Il est envoyé à votre ancienne adresse."),
            _ => new MailModel(
                "Email address change requested",
                "Your email address is being changed",
                ["A new email address was requested for your CircleTools account. Your current address stays active until the new one is confirmed. If this wasn't you, change your password immediately."],
                null,
                null,
                "For your security this notice never carries an action link. It's sent to your previous address."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderPasswordChanged(JsonElement root, string email, string language)
    {
        // The real owner hears about a password change even if someone else made it (FR-049). No link, by
        // design (NFR-005): the way back is "Forgot password", which they reach themselves.
        var brand = ResolveBranding(SourceApp(root));

        var model = language switch
        {
            "de" => new MailModel(
                "Dein Passwort wurde geändert",
                "Dein Passwort wurde geändert",
                ["Das Passwort deines CircleTools-Kontos wurde soeben geändert. Alle anderen Geräte wurden abgemeldet.", "Falls du das nicht warst, setze dein Passwort über «Passwort vergessen?» auf der Anmeldeseite sofort zurück."],
                null,
                null,
                "Aus Sicherheitsgründen enthält dieser Hinweis nie einen Aktionslink."),
            "fr" => new MailModel(
                "Votre mot de passe a été modifié",
                "Votre mot de passe a été modifié",
                ["Le mot de passe de votre compte CircleTools vient d'être modifié. Tous vos autres appareils ont été déconnectés.", "Si vous n'êtes pas à l'origine de ce changement, réinitialisez immédiatement votre mot de passe via « Mot de passe oublié ? » sur la page de connexion."],
                null,
                null,
                "Par sécurité, cet avis ne contient jamais de lien d'action."),
            _ => new MailModel(
                "Your password was changed",
                "Your password was changed",
                ["The password of your CircleTools account was just changed. All your other devices were signed out.", "If this wasn't you, reset your password right away with \"Forgot your password?\" on the sign-in page."],
                null,
                null,
                "For your security this notice never carries an action link."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderSignUpAttempt(JsonElement root, string email, string language)
    {
        // Sent to the existing account holder when someone tries to register their address. It must
        // never appear in the response to the person who tried (NFR-005), and it must not confirm
        // anything about the attempt beyond "it happened". No link, by design.
        var brand = ResolveBranding(SourceApp(root));

        var model = language switch
        {
            "de" => new MailModel(
                "Anmeldeversuch mit deiner E-Mail-Adresse",
                "Anmeldeversuch mit deiner E-Mail-Adresse",
                ["Jemand hat versucht, ein CircleTools-Konto mit deiner E-Mail-Adresse zu erstellen. Du hast bereits ein Konto – melde dich stattdessen an. Falls du das nicht warst, ist nichts passiert und du musst nichts unternehmen."],
                null,
                null,
                "Aus Sicherheitsgründen enthält dieser Hinweis nie einen Link. Du hast bereits ein Konto – melde dich einfach an."),
            "fr" => new MailModel(
                "Tentative d'inscription avec votre adresse e-mail",
                "Tentative d'inscription avec votre adresse e-mail",
                ["Quelqu'un a tenté de créer un compte CircleTools avec votre adresse e-mail. Vous avez déjà un compte — connectez-vous plutôt. Si ce n'était pas vous, rien ne s'est passé et vous n'avez rien à faire."],
                null,
                null,
                "Par sécurité, cet avis ne contient jamais de lien. Vous avez déjà un compte : connectez-vous simplement."),
            _ => new MailModel(
                "Sign-up attempt with your email address",
                "Sign-up attempt with your email address",
                ["Someone tried to create a CircleTools account with your email address. You already have an account — sign in instead. If this wasn't you, nothing happened and there's nothing you need to do."],
                null,
                null,
                "For your security this notice never carries a link. You already have an account — just sign in."),
        };

        return Compose(email, brand, language, model);
    }

    private OutboundMail RenderAccountDeleted(JsonElement root, string email, string language)
    {
        // A one-time farewell confirmation after deletion (FR-047, ADR-0064). No link, no assignment —
        // there is nothing to act on.
        var brand = ResolveBranding(SourceApp(root));

        var model = language switch
        {
            "de" => new MailModel(
                "Dein CircleTools-Konto wurde gelöscht",
                "Dein Konto wurde gelöscht",
                ["Dein CircleTools-Konto und alles, was damit verbunden war, wurden entfernt – genau wie von dir gewünscht, ohne versteckte Kopie. Danke, dass du Teil von CircleTools warst. Du bist jederzeit wieder willkommen."],
                null,
                null,
                "Dies ist eine einmalige Bestätigung. Du hörst nichts mehr von uns, es sei denn, du erstellst ein neues Konto."),
            "fr" => new MailModel(
                "Votre compte CircleTools a été supprimé",
                "Votre compte a été supprimé",
                ["Votre compte CircleTools et tout ce qui y était rattaché ont été supprimés, comme vous l'avez demandé — sans copie cachée. Merci d'avoir fait partie de CircleTools. Vous êtes le bienvenu à tout moment."],
                null,
                null,
                "Ceci est une confirmation unique. Vous n'aurez plus de nouvelles, sauf si vous créez un nouveau compte."),
            _ => new MailModel(
                "Your CircleTools account has been deleted",
                "Your account has been deleted",
                ["Your CircleTools account and everything tied to it have been removed, just as you asked — no hidden copy kept. Thank you for having been part of CircleTools. You're always welcome back."],
                null,
                null,
                "This is a one-time confirmation. You won't hear from us again unless you create a new account."),
        };

        return Compose(email, brand, language, model);
    }

    private static OutboundMail Compose(string email, MailBranding brand, string language, MailModel model)
    {
        var (html, text) = MailLayout.Render(brand, language, model);
        return new OutboundMail(email, model.Subject, text, html, brand.SenderName);
    }

    private static string? SourceApp(JsonElement root) =>
        root.TryGetProperty("sourceApp", out var s) ? s.GetString() : null;

    /// <summary>Resolves the source app's branding, falling back to the default app then CircleTools.</summary>
    private MailBranding ResolveBranding(string? sourceApp)
    {
        var opts = options.Value;

        MailApp? app = null;
        if (!string.IsNullOrWhiteSpace(sourceApp) && opts.Apps.TryGetValue(sourceApp, out var byName))
        {
            app = byName;
        }
        else if (!string.IsNullOrWhiteSpace(opts.DefaultApp) && opts.Apps.TryGetValue(opts.DefaultApp, out var byDefault))
        {
            app = byDefault;
        }

        var name = Coalesce(app?.Name, opts.FromName);
        var baseUrl = Coalesce(app?.BaseUrl, opts.ClientBaseUrl).TrimEnd('/');
        var color = Coalesce(app?.BrandColor, "#03173d");
        var sender = Coalesce(app?.SenderName, opts.FromName);
        var logo = string.IsNullOrWhiteSpace(app?.LogoUrl) ? null : app!.LogoUrl;

        return new MailBranding(name, baseUrl, color, logo, sender);
    }

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
