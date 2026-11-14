// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Resources;
using CircleTools.Ui.Shared;

namespace CircleTools.Ui.Shared.Test;

/// <summary>
/// Guards NFR-003 (DE/EN/FR) for the strings shared components render on their own behalf.
/// </summary>
/// <remarks>
/// The key-parity check is the same one each app runs over its own <c>AppStrings</c>. The neutral
/// check below is not, and it exists because key parity cannot see the bug that actually shipped:
/// the neutral set held German while the assembly declared its neutral language as English, so
/// every English screen rendered "Sprache" and every test stayed green.
/// </remarks>
public sealed class SharedStringsLocalizationTests
{
    private static readonly string[] _supportedCultures = ["de-CH", "en", "fr-CH"];

    private static readonly ResourceManager _resources =
        new("CircleTools.Ui.Shared.Resources.SharedStrings", typeof(SharedStrings).Assembly);

    private static Dictionary<string, string> EntriesFor(CultureInfo culture)
    {
        var set = _resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set!
            .Cast<DictionaryEntry>()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value as string))
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    [Fact]
    public void EverySupportedCulture_DefinesEveryNeutralKey()
    {
        var neutral = EntriesFor(CultureInfo.InvariantCulture);
        Assert.NotEmpty(neutral);

        foreach (var culture in _supportedCultures)
        {
            var keys = EntriesFor(new CultureInfo(culture)).Keys;
            var missing = neutral.Keys.Except(keys).OrderBy(key => key, StringComparer.Ordinal).ToList();

            Assert.True(missing.Count == 0, $"Culture '{culture}' is missing keys: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// The neutral resources must carry the text of the declared neutral language.
    /// </summary>
    /// <remarks>
    /// <c>Directory.Build.props</c> sets <c>NeutralLanguage=en</c>, which emits
    /// <see cref="System.Resources.NeutralResourcesLanguageAttribute"/>. The runtime then answers a
    /// lookup under culture <c>en</c> from the neutral set embedded in the main assembly and never
    /// opens the <c>en</c> satellite — so whatever sits here <em>is</em> the English UI, and a
    /// mismatch is invisible to every key-based check.
    /// </remarks>
    [Fact]
    public void NeutralResources_CarryTheEnglishText()
    {
        var neutral = EntriesFor(CultureInfo.InvariantCulture);
        var english = EntriesFor(new CultureInfo("en"));

        Assert.NotEmpty(neutral);

        var mismatched = neutral
            .Where(entry => english.TryGetValue(entry.Key, out var value) && value != entry.Value)
            .Select(entry => $"{entry.Key}: neutral='{entry.Value}' en='{english[entry.Key]}'")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            mismatched.Count == 0,
            "Neutral resources must match the en resources, because NeutralLanguage=en makes them " +
            $"the English UI: {string.Join("; ", mismatched)}");
    }
}
