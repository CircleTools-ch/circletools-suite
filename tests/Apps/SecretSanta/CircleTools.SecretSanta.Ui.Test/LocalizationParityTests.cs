// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
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

    // French writes a space before a colon, and it has to be a no-break one or the colon can wrap onto a line
    // of its own (BTP-8). It is the same space LabelText adds for labels the markup completes.
    [Fact]
    public void FrenchColons_FollowANoBreakSpace()
    {
        var set = _resources.GetResourceSet(new CultureInfo("fr-CH"), createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        var wrong = set!
            .Cast<DictionaryEntry>()
            .Where(entry => entry.Value is string value && Regex.IsMatch(value, "(?<! ):(?!//)", RegexOptions.None, TimeSpan.FromSeconds(1)))
            .Select(entry => (string)entry.Key)
            .ToList();

        Assert.True(wrong.Count == 0, $"French colons without a no-break space: {string.Join(", ", wrong)}");
    }
}
