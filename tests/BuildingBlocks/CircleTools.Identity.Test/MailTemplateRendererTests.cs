// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using CircleTools.Infrastructure.Mail;
using Microsoft.Extensions.Options;

namespace CircleTools.Identity.Test;

/// <summary>Covers the DE/EN/FR transactional mail templates (NFR-003, FR-007).</summary>
public sealed class MailTemplateRendererTests
{
    private static MailTemplateRenderer Renderer() =>
        new(Options.Create(new MailOptions { ClientBaseUrl = "https://app.example.test/" }));

    private static string VerificationPayload(string language) =>
        JsonSerializer.Serialize(new
        {
            userId = "0198a1f0-0000-7000-8000-000000000001",
            email = "anna@example.test",
            language,
            token = "abc123",
        });

    private static string InvitationPayload(string language, bool hasAccount) =>
        JsonSerializer.Serialize(new
        {
            email = "bea@example.test",
            language,
            circleName = "Müller",
            circleLabel = "Familie",
            inviterDisplayName = "Anna",
            secret = "s3cr3t-link-value",
            hasAccount,
        });

    [Theory]
    [InlineData("de-CH", "Bestätige")]
    [InlineData("fr-CH", "Confirmez")]
    [InlineData("en", "Confirm")]
    public void Verification_RendersInTheAccountsLanguage(string language, string expected)
    {
        var mail = Renderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload(language));

        Assert.NotNull(mail);
        Assert.Equal("anna@example.test", mail.To);
        Assert.Contains(expected, mail.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_BuildsAWorkingLink()
    {
        var mail = Renderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("de-CH"));

        Assert.NotNull(mail);
        Assert.Contains(
            "https://app.example.test/verify-email?userId=0198a1f0-0000-7000-8000-000000000001&token=abc123",
            mail.Body,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-CH")]
    [InlineData("fr-CH")]
    [InlineData("en")]
    public void SignUpAttempt_TellsTheOwnerWithoutConfirmingAnythingToTheRequester(string language)
    {
        var payload = JsonSerializer.Serialize(new { email = "anna@example.test", language });

        var mail = Renderer().Render(MailTemplateRenderer.SignUpAttemptType, payload);

        Assert.NotNull(mail);
        Assert.Equal("anna@example.test", mail.To);
        Assert.NotEmpty(mail.Subject);

        // It must not carry a verification link — that would let an attacker who can read the
        // victim's inbox complete a signup, and it would confirm the address is registered.
        Assert.DoesNotContain("verify-email", mail.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-CH", "Passwort wurde geändert")]
    [InlineData("fr-CH", "mot de passe a été modifié")]
    [InlineData("en", "password was changed")]
    public void PasswordChanged_TellsTheOwner_InTheirLanguage_WithoutALink(string language, string expected)
    {
        var payload = JsonSerializer.Serialize(new { email = "anna@example.test", language });

        var mail = Renderer().Render(MailTemplateRenderer.PasswordChangedType, payload);

        Assert.NotNull(mail);
        Assert.Equal("anna@example.test", mail.To);
        Assert.Contains(expected, mail.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", mail.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-CH", "lädt dich")]
    [InlineData("fr-CH", "vous invite")]
    [InlineData("en", "invites you")]
    public void InvitationTemplate_RendersInAllThreeLanguages(string language, string expected)
    {
        var mail = Renderer().Render(
            MailTemplateRenderer.InvitationType,
            InvitationPayload(language, hasAccount: false));

        Assert.NotNull(mail);
        Assert.Equal("bea@example.test", mail.To);
        Assert.Contains(expected, mail.Subject, StringComparison.Ordinal);
        Assert.Contains(expected, mail.Body, StringComparison.Ordinal);

        // What an invitation is allowed to say about the circle, and nothing more (UC-MAIN-010).
        Assert.Contains("Müller", mail.Body, StringComparison.Ordinal);
        Assert.Contains("Anna", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Invitation_ToAnAddressWithoutAnAccount_CarriesTheLink()
    {
        var mail = Renderer().Render(
            MailTemplateRenderer.InvitationType,
            InvitationPayload("de-CH", hasAccount: false));

        Assert.NotNull(mail);
        Assert.Contains(
            "https://app.example.test/invitation/s3cr3t-link-value",
            mail.Body,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The account path accepts by matching the caller's <em>verified</em> address, so its mail
    /// needs no token — and must not carry one. A secret in this mail would be a bearer key to
    /// someone else's circle the moment the mail is forwarded (UC-MAIN-010, FR-018).
    /// </summary>
    [Fact]
    public void Invitation_ToAnAddressWithAnAccount_PointsAtTheAppAndCarriesNoSecret()
    {
        var mail = Renderer().Render(
            MailTemplateRenderer.InvitationType,
            InvitationPayload("en", hasAccount: true));

        Assert.NotNull(mail);
        Assert.DoesNotContain("s3cr3t-link-value", mail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("/invitation/", mail.Body, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/invitations", mail.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A label is user data: it is stored as it was typed and never re-translated, so a French mail
    /// about a circle labelled "Chörli" says "Chörli" (FR-016).
    /// </summary>
    [Fact]
    public void Invitation_DoesNotRetranslateTheCirclesLabel()
    {
        var payload = JsonSerializer.Serialize(new
        {
            email = "bea@example.test",
            language = "fr-CH",
            circleName = "Müller",
            circleLabel = "Chörli",
            inviterDisplayName = "Anna",
            secret = "s3cr3t-link-value",
            hasAccount = false,
        });

        var mail = Renderer().Render(MailTemplateRenderer.InvitationType, payload);

        Assert.NotNull(mail);
        Assert.Contains("Chörli", mail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Famille", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UnknownType_ReturnsNull()
    {
        var mail = Renderer().Render("mail.not-a-real-type", VerificationPayload("en"));

        Assert.Null(mail);
    }

    [Fact]
    public void Render_PayloadWithoutRecipient_ReturnsNull()
    {
        var mail = Renderer().Render(MailTemplateRenderer.VerificationType, "{}");

        Assert.Null(mail);
    }

    [Theory]
    [InlineData("de-CH", "de")]
    [InlineData("de", "de")]
    [InlineData("fr-CH", "fr")]
    [InlineData("en-GB", "en")]
    [InlineData("it-CH", "en")] // unsupported culture falls back to English, not to a crash
    [InlineData(null, "de")] // de-CH is the product default
    [InlineData("", "de")]
    public void Normalize_ReducesCultureNamesToASupportedLanguage(string? input, string expected) =>
        Assert.Equal(expected, MailTemplateRenderer.Normalize(input));

    // --- ADR-0064: branded HTML, per-app identity, source-app link origin, farewell mail ---

    private static MailTemplateRenderer BrandedRenderer()
    {
        var options = new MailOptions { ClientBaseUrl = "https://circletools.test/", DefaultApp = "circletools" };
        options.Apps["circletools"] = new MailApp
        {
            Name = "CircleTools",
            BaseUrl = "https://circletools.test",
            BrandColor = "#03173d",
            SenderName = "CircleTools",
        };
        options.Apps["secretsanta"] = new MailApp
        {
            Name = "Secret Santa",
            BaseUrl = "https://secretsanta.test",
            BrandColor = "#7a1f2b",
            SenderName = "Secret Santa · CircleTools",
        };
        return new MailTemplateRenderer(Options.Create(options));
    }

    private static string VerificationPayload(string language, string? sourceApp) =>
        JsonSerializer.Serialize(new
        {
            userId = "0198a1f0-0000-7000-8000-000000000001",
            email = "anna@example.test",
            language,
            token = "abc123",
            sourceApp,
        });

    [Fact]
    public void Verification_ProducesBrandedHtml_WithCtaButtonAndFallbackLink()
    {
        var mail = Renderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);

        // The CTA button's href is the exact link (with the ampersand HTML-encoded in the attribute).
        Assert.Contains(
            "href=\"https://app.example.test/verify-email?userId=0198a1f0-0000-7000-8000-000000000001&amp;token=abc123\"",
            mail.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains("Confirm email", mail.HtmlBody, StringComparison.Ordinal);
        // The full link is still shown as a fallback for when the button doesn't work.
        Assert.Contains("Button not working?", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_FromSecretSanta_UsesSecretSantaBrandingSenderAndOrigin()
    {
        var mail = BrandedRenderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en", "secretsanta"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        Assert.Contains("#7a1f2b", mail.HtmlBody, StringComparison.Ordinal); // festive-red brand colour
        Assert.Contains("Secret Santa", mail.HtmlBody, StringComparison.Ordinal); // wordmark
        Assert.Equal("Secret Santa · CircleTools", mail.FromName); // co-branded inbox sender
        // The link returns to the Secret-Santa origin, so the page renders in that app's shell.
        Assert.Contains("https://secretsanta.test/verify-email", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_FromCircleTools_UsesNavyAndCircleToolsOrigin()
    {
        var mail = BrandedRenderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en", "circletools"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        Assert.Contains("#03173d", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("CircleTools", mail.FromName);
        Assert.Contains("https://circletools.test/verify-email", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownSourceApp_FallsBackToTheDefaultBranding()
    {
        var mail = BrandedRenderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en", "does-not-exist"));

        Assert.NotNull(mail);
        Assert.Equal("CircleTools", mail.FromName);
        Assert.Contains("https://circletools.test/verify-email", mail.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-CH", "gelöscht")]
    [InlineData("fr-CH", "supprimé")]
    [InlineData("en", "deleted")]
    public void AccountDeleted_RendersFarewell_InTheAccountsLanguage_WithNoLink(string language, string expected)
    {
        var payload = JsonSerializer.Serialize(new { email = "anna@example.test", language, sourceApp = (string?)null });

        var mail = BrandedRenderer().Render(MailTemplateRenderer.AccountDeletedType, payload);

        Assert.NotNull(mail);
        Assert.Equal("anna@example.test", mail.To);
        Assert.Contains(expected, mail.Subject, StringComparison.Ordinal);
        Assert.NotNull(mail.HtmlBody);
        // A confirmation has nothing to act on: no action link in either body.
        Assert.DoesNotContain("http://", mail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_EncodesUserSuppliedText_SoNamesCannotInjectMarkup()
    {
        var payload = JsonSerializer.Serialize(new
        {
            email = "bea@example.test",
            language = "en",
            circleName = "<script>evil()</script>",
            circleLabel = "Family",
            inviterDisplayName = "Anna",
            secret = "s3cr3t-link-value",
            hasAccount = false,
        });

        var mail = Renderer().Render(MailTemplateRenderer.InvitationType, payload);

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        Assert.DoesNotContain("<script>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_RendersTheLogoImage_WhenLogoUrlIsConfigured()
    {
        var options = new MailOptions { ClientBaseUrl = "https://x.test/", DefaultApp = "ss" };
        options.Apps["ss"] = new MailApp
        {
            Name = "Secret Santa",
            BaseUrl = "https://secretsanta.test",
            BrandColor = "#7a1f2b",
            SenderName = "Secret Santa · CircleTools",
            LogoUrl = "https://secretsanta.test/_content/CircleTools.Ui.Shared/img/email-logo-secretsanta.png",
        };
        var renderer = new MailTemplateRenderer(Options.Create(options));

        var mail = renderer.Render(MailTemplateRenderer.VerificationType, VerificationPayload("en", "ss"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        Assert.Contains(
            "<img src=\"https://secretsanta.test/_content/CircleTools.Ui.Shared/img/email-logo-secretsanta.png\"",
            mail.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains("alt=\"Secret Santa\"", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_OmitsTheLogoImage_WhenNoLogoUrlIsConfigured()
    {
        // BrandedRenderer's apps set no LogoUrl → wordmark-only header, no broken image.
        var mail = BrandedRenderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en", "secretsanta"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        Assert.DoesNotContain("<img", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_IsFluidWidth_SoItFitsAPhoneWithoutSidewaysScroll()
    {
        var mail = Renderer().Render(MailTemplateRenderer.VerificationType, VerificationPayload("en"));

        Assert.NotNull(mail);
        Assert.NotNull(mail.HtmlBody);
        // The card shrinks to fit rather than forcing a fixed 600px.
        Assert.Contains("width:100%;max-width:600px", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("width=device-width", mail.HtmlBody, StringComparison.Ordinal);
        // Phone media query tightens the gutters.
        Assert.Contains("max-width:480px", mail.HtmlBody, StringComparison.Ordinal);
    }
}
