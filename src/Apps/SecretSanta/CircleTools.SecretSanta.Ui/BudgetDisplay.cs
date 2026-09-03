using System.Globalization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Formats a gift budget for display. A budget is always a maximum; when a minimum is also set it is
/// shown as a range (e.g. <c>10–30 CHF</c>), otherwise as the bare maximum (e.g. <c>30 CHF</c>) — FR-036.
/// The amounts are formatted for the active culture; the currency code follows the value.
/// </summary>
public static class BudgetDisplay
{
    private const string EnDash = "–";

    /// <summary>Formats a budget for the current culture.</summary>
    /// <param name="minAmount">The optional minimum (lower bound); <see langword="null"/> for a bare maximum.</param>
    /// <param name="maxAmount">The maximum (upper limit).</param>
    /// <param name="currency">The ISO currency code, or <see langword="null"/>/empty to omit it.</param>
    /// <returns>The display string, e.g. <c>10–30 CHF</c> or <c>30 CHF</c>.</returns>
    public static string Format(decimal? minAmount, decimal maxAmount, string? currency)
    {
        var max = maxAmount.ToString("0.##", CultureInfo.CurrentCulture);
        var value = minAmount is { } min
            ? $"{min.ToString("0.##", CultureInfo.CurrentCulture)}{EnDash}{max}"
            : max;

        return string.IsNullOrWhiteSpace(currency) ? value : $"{value} {currency}";
    }
}
