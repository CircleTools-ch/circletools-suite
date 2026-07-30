using System.Text.Json;
using AppSuite.Infrastructure.Mail;
using Microsoft.Extensions.Options;

namespace AppSuite.Identity.Test;

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
}
