using System.Globalization;
using System.Resources;
using AppSuite.SecretSanta.Ui;

namespace AppSuite.SecretSanta.Ui.Test;

/// <summary>
/// The claim list's member count at exactly one (TP-004). The count is two keys, not "{0} members"
/// with a swapped number: at one the noun is singular in every language this app speaks, and reusing
/// the plural produces "1 Personen" / "1 members" — the M2 trap this test exists to keep shut.
/// </summary>
public sealed class ClaimMemberCountTests
{
    private static readonly ResourceManager _resources =
        new("AppSuite.SecretSanta.Ui.Resources.AppStrings", typeof(AppStrings).Assembly);

    public static TheoryData<string, string, string> CountAtOne => new()
    {
        // culture, expected at one (singular), expected at three (plural)
        { "en", "1 member", "3 members" },
        { "de-CH", "1 Person", "3 Personen" },
        { "fr-CH", "1 membre", "3 membres" },
    };

    [Theory]
    [MemberData(nameof(CountAtOne))]
    public void MemberCount_UsesTheSingularAtOne_AndThePluralAboveOne(
        string cultureName,
        string expectedAtOne,
        string expectedAtThree)
    {
        var culture = new CultureInfo(cultureName);

        var singular = _resources.GetString("ClaimMemberCountSingular", culture);
        var plural = _resources.GetString("ClaimMemberCountPlural", culture);

        Assert.NotNull(singular);
        Assert.NotNull(plural);
        Assert.Contains("{0}", singular);
        Assert.Contains("{0}", plural);
        Assert.NotEqual(singular, plural);

        // The page picks the key with count == 1; this asserts the chosen strings are grammatical.
        Assert.Equal(expectedAtOne, string.Format(culture, singular!, 1));
        Assert.Equal(expectedAtThree, string.Format(culture, plural!, 3));
    }
}
