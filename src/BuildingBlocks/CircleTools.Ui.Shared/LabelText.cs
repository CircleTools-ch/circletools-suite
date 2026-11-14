// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace CircleTools.Ui.Shared;

/// <summary>
/// Punctuation that differs per UI language, for labels the markup completes (<c>Budget: CHF 20–50</c>).
/// </summary>
/// <remarks>
/// French sets a space before the colon (« Budget : »), German and English do not. The colon used to be typed
/// into the markup after the localized label, so French read « Pour: Anna » next to strings that had it right
/// (2026-10-28). The space is a no-break space, so the colon never wraps onto a line of its own.
/// </remarks>
public static class LabelText
{
    private const string FrenchColon = " :";

    /// <summary>The label followed by a colon, spaced the way the current UI culture writes it.</summary>
    /// <param name="label">The localized label, without punctuation.</param>
    /// <returns>The label and its colon.</returns>
    public static string WithColon(string label) => WithColon(label, CultureInfo.CurrentUICulture);

    /// <summary>The label followed by a colon, spaced the way <paramref name="culture"/> writes it.</summary>
    /// <param name="label">The localized label, without punctuation.</param>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The label and its colon.</returns>
    public static string WithColon(string label, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TwoLetterISOLanguageName == "fr" ? label + FrenchColon : label + ":";
    }
}
