// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace CircleTools.Localization;

/// <summary>
/// The product languages (ADR-0011). Swiss-first: <c>de-CH</c> is the default, and the suite ships
/// <c>de-CH</c>, <c>en</c> and <c>fr-CH</c>.
/// </summary>
/// <remarks>
/// This is a registry of constants, not behaviour, which is why it is static: there is nothing here
/// to inject, fake, or vary per host. Where the choice is *stored* does vary, and that lives in
/// <see cref="CultureOptions"/>.
/// </remarks>
public static class AppCultures
{
    /// <summary>The culture used when the user has not chosen one (Swiss-first).</summary>
    public const string DefaultCulture = "de-CH";

    /// <summary>The cultures offered in the UI, in display order.</summary>
    public static IReadOnlyList<string> Supported { get; } = ["de-CH", "en", "fr-CH"];

    /// <summary>
    /// Resolves a culture name to a supported <see cref="CultureInfo"/>, falling back to
    /// <see cref="DefaultCulture"/> for anything missing or unrecognised.
    /// </summary>
    /// <remarks>
    /// An exact (canonical) match wins first, so a stored <c>de-CH</c> / <c>en</c> / <c>fr-CH</c> resolves
    /// to itself. Otherwise the two-letter language is matched, so a device/browser culture such as
    /// <c>de-DE</c>, <c>fr-FR</c> or <c>en-GB</c> maps to the supported culture that speaks the same
    /// language (used for the first-run device-language default). Anything else falls back to
    /// <see cref="DefaultCulture"/>.
    /// </remarks>
    /// <param name="name">The culture name, which may be <see langword="null"/> or unknown.</param>
    /// <returns>A supported culture.</returns>
    public static CultureInfo Resolve(string? name)
    {
        var exact = Supported.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return new CultureInfo(exact);
        }

        var language = TwoLetterLanguage(name);
        var byLanguage = language is null
            ? null
            : Supported.FirstOrDefault(c => string.Equals(TwoLetterLanguage(c), language, StringComparison.Ordinal));

        return new CultureInfo(byLanguage ?? DefaultCulture);
    }

    // The two-letter language of a culture name, lower-cased, by taking the part before the region
    // separator. Deliberately string-based rather than constructing a CultureInfo, which can throw on a
    // malformed or region-only device value (and needs globalization data this app does not carry).
    private static string? TwoLetterLanguage(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        var separator = cultureName.IndexOfAny(['-', '_']);
        return (separator >= 0 ? cultureName[..separator] : cultureName).ToLowerInvariant();
    }

    /// <summary>Gets the display name shown in the language picker for a supported culture.</summary>
    /// <param name="name">The culture name.</param>
    /// <returns>The native display name (for example "Deutsch").</returns>
    public static string DisplayName(string name) => name switch
    {
        "de-CH" => "Deutsch",
        "en" => "English",
        "fr-CH" => "Français",
        _ => name,
    };

    /// <summary>
    /// Normalises a culture name to the form the account profile stores, so a language chosen in the
    /// UI and a language read back from the server are always comparable.
    /// </summary>
    /// <param name="name">The culture name, which may be <see langword="null"/> or unknown.</param>
    /// <returns>A supported culture name.</returns>
    public static string Normalize(string? name) => Resolve(name).Name;
}
