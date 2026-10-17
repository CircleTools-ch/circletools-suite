using System.Globalization;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// One budget format everywhere (FR-036; Q28, 2026-10-16 device test): a maximum reads as a limit ("up to"),
/// a range and an exact amount read plainly, and the currency sits where the region puts it.
/// </summary>
public sealed class BudgetDisplayTests
{
    [Theory]
    [InlineData("de-CH", null, 30, "bis CHF 30")]
    [InlineData("de-CH", 20.0, 30, "CHF 20–30")]
    [InlineData("de-CH", 30.0, 30, "CHF 30")]
    [InlineData("de-CH", 12.5, 30, "CHF 12.5–30")]
    [InlineData("en", null, 30, "up to CHF 30")]
    [InlineData("en", 20.0, 30, "CHF 20–30")]
    [InlineData("en", 30.0, 30, "CHF 30")]
    [InlineData("fr-CH", null, 30, "jusqu’à 30 CHF")]
    [InlineData("fr-CH", 20.0, 30, "20–30 CHF")]
    [InlineData("fr-CH", 30.0, 30, "30 CHF")]
    public void FormatsPerLocale(string culture, double? min, double max, string expected)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal(expected, BudgetDisplay.Format((decimal?)min, (decimal)max, "CHF"));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
    }

    [Fact]
    public void WithoutACurrency_OnlyTheAmountIsShown()
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            Assert.Equal("10–30", BudgetDisplay.Format(10m, 30m, null));
            Assert.Equal("up to 30", BudgetDisplay.Format(null, 30m, " "));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
    }
}
