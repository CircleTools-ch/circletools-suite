using System.Collections;
using System.Globalization;
using System.Resources;

namespace AppSuite.Main.Ui.Test;

/// <summary>
/// Guards NFR-003 (DE/EN/FR): every string key defined in the neutral <c>AppStrings</c> resources
/// must be present and non-empty in each supported culture, so no screen falls back silently.
/// </summary>
public sealed class LocalizationParityTests
{
    private static readonly string[] _supportedCultures = ["de-CH", "en", "fr-CH"];

    private static readonly ResourceManager _resources =
        new("AppSuite.Main.Ui.Resources.AppStrings", typeof(AppStrings).Assembly);

    private static HashSet<string> KeysFor(CultureInfo culture)
    {
        var set = _resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set!
            .Cast<DictionaryEntry>()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value as string))
            .Select(entry => (string)entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void EverySupportedCulture_DefinesEveryNeutralKey()
    {
        var neutralKeys = KeysFor(CultureInfo.InvariantCulture);
        Assert.NotEmpty(neutralKeys);

        foreach (var culture in _supportedCultures)
        {
            var cultureKeys = KeysFor(new CultureInfo(culture));
            var missing = neutralKeys.Except(cultureKeys).OrderBy(key => key, StringComparer.Ordinal).ToList();

            Assert.True(missing.Count == 0, $"Culture '{culture}' is missing keys: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// A translation that drops a placeholder loses the value the sentence was written to carry, and
    /// key parity cannot see it: the key is present and the string is non-empty, it simply no longer
    /// says which circle, or how many people.
    /// </summary>
    /// <remarks>
    /// This became load-bearing with FR-022's deletion confirmation, where the placeholders <em>are</em>
    /// the acceptance criteria — the circle's name, the count of people who lose access, the managed
    /// members by name. A German or French value without its <c>{0}</c> would render a sentence that
    /// reads perfectly well and names nothing.
    /// </remarks>
    [Fact]
    public void EveryPlaceholderInANeutralString_SurvivesIntoEveryLanguage()
    {
        var offenders = new List<string>();

        foreach (var (key, neutral) in ValuesFor(CultureInfo.InvariantCulture))
        {
            var expected = Placeholders(neutral);

            if (expected.Count == 0)
            {
                continue;
            }

            foreach (var culture in _supportedCultures)
            {
                var translated = ValuesFor(new CultureInfo(culture)).GetValueOrDefault(key) ?? string.Empty;
                var missing = expected.Except(Placeholders(translated)).ToList();

                if (missing.Count > 0)
                {
                    offenders.Add($"{culture}/{key} is missing {string.Join(", ", missing)}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static Dictionary<string, string> ValuesFor(CultureInfo culture)
    {
        var set = _resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        return set!
            .Cast<DictionaryEntry>()
            .Where(entry => entry.Value is string)
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    /// <summary>The <c>{0}</c>-style placeholders in a string, whatever order they appear in.</summary>
    private static HashSet<string> Placeholders(string value) =>
        System.Text.RegularExpressions.Regex
            .Matches(value, @"\{\d+")
            .Select(match => match.Value + "}")
            .ToHashSet(StringComparer.Ordinal);
}
