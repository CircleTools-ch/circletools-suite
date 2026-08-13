using System.Collections;
using System.Globalization;
using System.Resources;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Guards NFR-003 (DE/EN/FR): every string key defined in the neutral <c>AppStrings</c> resources
/// must be present and non-empty in each supported culture, so no screen falls back silently.
/// </summary>
public sealed class LocalizationParityTests
{
    private static readonly string[] _supportedCultures = ["de-CH", "en", "fr-CH"];

    private static readonly ResourceManager _resources =
        new("CircleTools.SecretSanta.Ui.Resources.AppStrings", typeof(AppStrings).Assembly);

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
}
