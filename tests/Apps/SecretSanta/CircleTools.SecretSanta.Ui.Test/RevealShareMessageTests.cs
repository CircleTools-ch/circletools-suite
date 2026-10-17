using System.Globalization;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Ui;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The share message carries its context (Q32a, 2026-10-16 device test): who it is for, the group and occasion,
/// date, place and budget, that only they see it — and the link last, on its own line.
/// </summary>
public sealed class RevealShareMessageTests
{
    private static StringLocalizer<AppStrings> Strings() =>
        new StringLocalizer<AppStrings>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    [Fact]
    public void AFullyDescribedOccasion_ReadsInGerman()
    {
        var (culture, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-CH");
            var occasion = new EventDto(
                Guid.NewGuid(), Guid.NewGuid(), "Weihnachten 2026", new BudgetDto(30m, "CHF"),
                new EventDetailsDto(new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero), "Bei Oma", string.Empty),
                true, [], false, []);

            var text = RevealShareMessage.Compose(Strings(), "Anna", "Familie", occasion, "https://example.test/r#x");

            Assert.Equal(
                "Hallo Anna! 🎁\n"
                + "Hier ist dein Wichtel-Link für «Familie» – Weihnachten 2026.\n"
                + "📅 24.12.2026 · 📍 Bei Oma · 💰 bis CHF 30\n"
                + "Öffne ihn, um zu sehen, wen du beschenkst – nur du siehst es.\n"
                + "https://example.test/r#x",
                text);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, ui);
        }
    }

    [Fact]
    public void WithoutDetails_TheFactsLineIsLeftOut_AndTheLinkIsStillLast()
    {
        var (culture, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            var text = RevealShareMessage.Compose(Strings(), "Ben", "Family", null, "https://example.test/r#y");
            var lines = text.Split('\n');

            Assert.Equal(4, lines.Length);
            Assert.Equal("Hi Ben! 🎁", lines[0]);
            Assert.DoesNotContain("📅", text);
            Assert.Equal("https://example.test/r#y", lines[^1]);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, ui);
        }
    }
}
