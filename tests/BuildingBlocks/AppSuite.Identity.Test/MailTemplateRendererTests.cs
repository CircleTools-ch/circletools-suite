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

    [Theory]
    [InlineData("de-CH", "Bestätigen")]
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
