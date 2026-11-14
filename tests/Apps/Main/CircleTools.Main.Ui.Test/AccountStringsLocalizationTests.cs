// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using CircleTools.Ui.Account;

namespace CircleTools.Main.Ui.Test;

/// <summary>
/// Guards NFR-003 (DE/EN/FR) for the shared account/session block's own strings — including the
/// three cross-app SSO strings (FEAT-006 / TP-006): the "continue" affordance, the "signing you
/// in…" status, and the neutral fallback message.
/// </summary>
/// <remarks>
/// The block lives in <c>CircleTools.Ui.Account</c> and has no test project of its own, so its
/// resource parity is checked here, where its component tests already run. Without this, a connect
/// string missing from <c>de-CH</c> or <c>fr-CH</c> would fall back silently and pass every other
/// test — the same class of bug the shared-strings guard was written for.
/// </remarks>
public sealed class AccountStringsLocalizationTests
{
    private static readonly string[] _supportedCultures = ["de-CH", "en", "fr-CH"];

    private static readonly ResourceManager _resources =
        new("CircleTools.Ui.Account.Resources.AccountStrings", typeof(AccountStrings).Assembly);

    // The SSO strings this milestone adds; named so a dropped one fails here by name (TP-006).
    private static readonly string[] _connectKeys =
        ["ConnectTitle", "ConnectWorking", "ConnectFailedTitle", "ConnectFailedText", "ConnectContinue"];

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

    [Fact]
    public void EverySsoString_IsDefinedInEveryLanguage()
    {
        foreach (var culture in _supportedCultures)
        {
            var entries = EntriesFor(new CultureInfo(culture));
            var missing = _connectKeys.Where(key => !entries.ContainsKey(key)).ToList();

            Assert.True(missing.Count == 0, $"Culture '{culture}' is missing SSO strings: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// The neutral resources carry the English text, because <c>NeutralLanguage=en</c> makes the
    /// neutral set the English UI (see the shared-strings guard for the bug this catches).
    /// </summary>
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
            $"Neutral resources must match the en resources: {string.Join("; ", mismatched)}");
    }

    /// <summary>A placeholder dropped in translation loses the value the sentence carries, invisibly to key parity.</summary>
    [Fact]
    public void EveryPlaceholderInANeutralString_SurvivesIntoEveryLanguage()
    {
        var offenders = new List<string>();

        foreach (var (key, neutral) in EntriesFor(CultureInfo.InvariantCulture))
        {
            var expected = Placeholders(neutral);

            if (expected.Count == 0)
            {
                continue;
            }

            foreach (var culture in _supportedCultures)
            {
                var translated = EntriesFor(new CultureInfo(culture)).GetValueOrDefault(key) ?? string.Empty;
                var missing = expected.Except(Placeholders(translated)).ToList();

                if (missing.Count > 0)
                {
                    offenders.Add($"{culture}/{key} is missing {string.Join(", ", missing)}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static HashSet<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+").Select(match => match.Value + "}").ToHashSet(StringComparer.Ordinal);
}
