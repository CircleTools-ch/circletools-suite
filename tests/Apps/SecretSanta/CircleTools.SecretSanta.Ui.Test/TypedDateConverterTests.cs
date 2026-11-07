using System.Globalization;
using MudBlazor.Utilities.Exceptions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// PR-9: a typed date is read in the region's order however it is separated, and a text that is no date
/// fails with the key the picker localizes, not with English text.
/// </summary>
public sealed class TypedDateConverterTests : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    private static readonly DateTime ChristmasEve = new(2026, 12, 24);

    [Theory]
    [InlineData("de-CH", "24.12.2026")]
    [InlineData("de-CH", "24-12-2026")]
    [InlineData("de-CH", "24/12/2026")]
    [InlineData("de-CH", "24.12.26")]
    [InlineData("de-CH", "24.12.2026.")]
    [InlineData("de-CH", " 24. 12. 2026 ")]
    [InlineData("fr-CH", "24.12.2026")]
    [InlineData("en-GB", "24/12/2026")]
    [InlineData("en-GB", "24.12.2026")]
    [InlineData("en-GB", "24-12-2026")]
    [InlineData("en-US", "12/24/2026")]
    [InlineData("en-US", "12.24.2026")]
    [InlineData("en-US", "12-24-2026")]
    [InlineData("de-CH", "2026-12-24")]
    [InlineData("en-US", "2026-12-24")]
    public void ReadsTheDate(string region, string text)
    {
        Assert.True(TypedDateConverter.TryParse(text, CultureInfo.GetCultureInfo(region), out var date));
        Assert.Equal(ChristmasEve, date);
    }

    [Fact]
    public void AcceptsOneDigitDaysAndMonths()
    {
        Assert.True(TypedDateConverter.TryParse("1.2.2026", CultureInfo.GetCultureInfo("de-CH"), out var date));
        Assert.Equal(new DateTime(2026, 2, 1), date);
    }

    [Theory]
    [InlineData("de-CH", 2026, 4, 3)]
    [InlineData("en-GB", 2026, 4, 3)]
    [InlineData("en-US", 2026, 3, 4)]
    public void TheRegionDecidesTheOrder_WhicheverSeparatorIsUsed(string region, int year, int month, int day)
    {
        Assert.True(TypedDateConverter.TryParse("03.04.2026", CultureInfo.GetCultureInfo(region), out var date));
        Assert.Equal(new DateTime(year, month, day), date);
    }

    [Theory]
    [InlineData("de-CH", "2026. 12. 24.")]
    [InlineData("ko-KR", "2026-12-24")]
    public void AYearFirstDateIsRead(string region, string text)
    {
        Assert.True(TypedDateConverter.TryParse(text, CultureInfo.GetCultureInfo(region), out var date));
        Assert.Equal(ChristmasEve, date);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("24.12")]
    [InlineData("24.12.2026.1")]
    [InlineData("31.02.2026")]
    [InlineData("24.13.2026")]
    [InlineData("24.12.202")]
    [InlineData("24.12.20266")]
    [InlineData("a.b.c")]
    [InlineData("٢٤.١٢.٢٠٢٦")]
    public void RefusesWhatIsNoDate(string text) =>
        Assert.False(TypedDateConverter.TryParse(text, CultureInfo.GetCultureInfo("de-CH"), out _));

    [Fact]
    public void AnAmericanRegionDoesNotGuessAtADayFirstDate() =>
        Assert.False(TypedDateConverter.TryParse("24.12.2026", CultureInfo.GetCultureInfo("en-US"), out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyFieldIsNoDate_NotAnError(string? text) =>
        Assert.Null(TypedDateConverter.Instance.ConvertBack(text));

    [Fact]
    public void AFailureCarriesTheKeyThePickerLocalizes()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");

        var failure = Assert.Throws<ConversionException>(() => TypedDateConverter.Instance.ConvertBack("hello"));

        Assert.Equal("Converter_InvalidDateTime", failure.ErrorMessageKey);
    }

    [Fact]
    public void ShowsTheDateAsTheRegionWritesIt_AndReadsItBack()
    {
        foreach (var region in new[] { "de-CH", "fr-CH", "en-GB", "en-US", "ko-KR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);

            var shown = TypedDateConverter.Instance.Convert(ChristmasEve);

            Assert.Equal(ChristmasEve, TypedDateConverter.Instance.ConvertBack(shown));
        }

        Assert.Null(TypedDateConverter.Instance.Convert(null));
    }

    /// <inheritdoc />
    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
