using System.Globalization;
using CircleTools.SecretSanta.Contracts;
using Microsoft.Extensions.Localization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The message a reveal link is shared with (Q32a, 2026-10-16 device test): who it is for, which group and
/// occasion, when and where, the budget in the one budget format, and that only they see who they got — so
/// the recipient knows what the link is before opening it. The link is the last line, on its own, because
/// some share targets read only the text.
/// </summary>
public static class RevealShareMessage
{
    /// <summary>Builds the message.</summary>
    /// <param name="l">The app strings.</param>
    /// <param name="forName">The person the link is for.</param>
    /// <param name="groupName">The group's name.</param>
    /// <param name="occasion">The occasion, for its name, date, place and budget; <see langword="null"/> when unknown.</param>
    /// <param name="url">The reveal link.</param>
    /// <returns>The text to share.</returns>
    public static string Compose(IStringLocalizer<AppStrings> l, string forName, string groupName, EventDto? occasion, string url)
    {
        ArgumentNullException.ThrowIfNull(l);

        var lines = new List<string> { l["ShareMessageGreeting", forName.Trim()] };
        var eventName = string.IsNullOrWhiteSpace(occasion?.Name) ? l["DefaultEventName"].Value : occasion!.Name.Trim();
        lines.Add(string.IsNullOrWhiteSpace(groupName)
            ? l["ShareMessageForEvent", eventName]
            : l["ShareMessageForGroupEvent", groupName.Trim(), eventName]);

        var facts = new List<string>(3);
        if (occasion?.Details?.Date is { } date)
        {
            facts.Add($"📅 {date.ToString("d", CultureInfo.CurrentCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(occasion?.Details?.Location))
        {
            facts.Add($"📍 {occasion.Details.Location.Trim()}");
        }

        if (occasion?.Budget is { } budget)
        {
            facts.Add($"💰 {BudgetDisplay.Format(budget.MinAmount, budget.Amount, budget.Currency)}");
        }

        if (facts.Count > 0)
        {
            lines.Add(string.Join(" · ", facts));
        }

        lines.Add(l["ShareMessageSecret"]);
        lines.Add(url);
        return string.Join("\n", lines);
    }
}
