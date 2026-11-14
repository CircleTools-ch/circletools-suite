// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Resources;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Formats a gift budget the one way it is shown everywhere — reveal, my assignments, details, calendar and
/// share text (FR-036; Q28, 2026-10-16 device test). A budget always has a maximum and may have a minimum:
/// <list type="bullet">
///   <item>maximum only → "up to CHF 30" (it is a limit, not a price);</item>
///   <item>minimum and maximum → "CHF 20–30";</item>
///   <item>minimum equal to the maximum → "CHF 30".</item>
/// </list>
/// The currency sits where the user's region puts it — before the amount in de-CH and English ("CHF 30"),
/// after it in fr-CH ("30 CHF") — taken from the culture's currency pattern. Amounts use the culture's
/// number format.
/// </summary>
public static class BudgetDisplay
{
    private const string EnDash = "–";

    private static readonly ResourceManager Strings =
        new("CircleTools.SecretSanta.Ui.Resources.AppStrings", typeof(AppStrings).Assembly);

    /// <summary>Formats a budget for the current culture.</summary>
    /// <param name="minAmount">The optional minimum (lower bound); <see langword="null"/> for a bare maximum.</param>
    /// <param name="maxAmount">The maximum (upper limit).</param>
    /// <param name="currency">The ISO currency code, or <see langword="null"/>/empty to omit it.</param>
    /// <returns>The display string, e.g. <c>CHF 20–30</c>, <c>up to CHF 30</c> or <c>CHF 30</c>.</returns>
    public static string Format(decimal? minAmount, decimal maxAmount, string? currency)
    {
        var culture = CultureInfo.CurrentCulture;
        var max = maxAmount.ToString("0.##", culture);
        if (minAmount is { } min && min != maxAmount)
        {
            return WithCurrency($"{min.ToString("0.##", culture)}{EnDash}{max}", currency, culture);
        }

        var amount = WithCurrency(max, currency, culture);
        if (minAmount is not null)
        {
            return amount; // min == max: an exact amount
        }

        var upTo = Strings.GetString("BudgetUpTo", CultureInfo.CurrentUICulture) ?? "{0}";
        return string.Format(culture, upTo, amount);
    }

    // Patterns 0 ("¤n") and 2 ("¤ n") put the currency first; 1 and 3 put it after.
    private static string WithCurrency(string value, string? currency, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return value;
        }

        return culture.NumberFormat.CurrencyPositivePattern is 0 or 2 ? $"{currency} {value}" : $"{value} {currency}";
    }
}
