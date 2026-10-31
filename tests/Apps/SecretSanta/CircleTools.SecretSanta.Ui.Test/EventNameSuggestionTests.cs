using System.Globalization;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// A new event's suggested name skips the years the group already has (BTP-5): the second event suggests
/// "Christmas 2027", not a duplicate "Christmas 2026".
/// </summary>
public sealed class EventNameSuggestionTests
{
    [Fact]
    public void WithoutEvents_TheCurrentYearIsSuggested() =>
        Assert.Equal("Christmas 2026", EventNameSuggestion.Next("Christmas {0}", 2026, []));

    [Fact]
    public void WhenTheCurrentYearIsTaken_TheNextYearIsSuggested() =>
        Assert.Equal("Christmas 2027", EventNameSuggestion.Next("Christmas {0}", 2026, ["Christmas 2026"]));

    [Fact]
    public void TakenYearsAreSkipped_UntilAFreeOne() =>
        Assert.Equal(
            "Christmas 2029",
            EventNameSuggestion.Next("Christmas {0}", 2026, ["Christmas 2027", "Christmas 2026", "Christmas 2028"]));

    [Fact]
    public void AFreeCurrentYearIsSuggested_EvenWhenLaterYearsAreTaken() =>
        Assert.Equal("Christmas 2026", EventNameSuggestion.Next("Christmas {0}", 2026, ["Christmas 2027"]));

    [Fact]
    public void NamesCompareTrimmedAndIgnoringCase() =>
        Assert.Equal("Christmas 2027", EventNameSuggestion.Next("Christmas {0}", 2026, ["  christmas 2026 "]));

    [Fact]
    public void OtherNamesDoNotCount() =>
        Assert.Equal("Christmas 2026", EventNameSuggestion.Next("Christmas {0}", 2026, ["Office party", "", "2026"]));

    [Fact]
    public void TheLocalizedFormatIsUsed()
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CH");

            Assert.Equal("Noël 2027", EventNameSuggestion.Next("Noël {0}", 2026, ["Noël 2026"]));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
    }
}
