using System.Globalization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The name suggested for a new event: the localized default occasion for a year ("Christmas 2026" /
/// "Weihnachten 2026" / "Noël 2026"), starting at the current year and moving on to the first year the group
/// has no event of that name for — so adding a second event suggests "Christmas 2027" instead of a duplicate
/// "Christmas 2026" (BTP-5, 2026-10-30). Names compare trimmed and case-insensitively.
/// </summary>
public static class EventNameSuggestion
{
    /// <summary>Suggests the name for a new event, in the current culture.</summary>
    /// <param name="format">The localized default-name format; <c>{0}</c> is the year.</param>
    /// <param name="currentYear">The current year, the earliest one suggested.</param>
    /// <param name="existingNames">The names of the group's events.</param>
    /// <returns>The first default name not taken by an existing event.</returns>
    public static string Next(string format, int currentYear, IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(existingNames);

        var taken = new HashSet<string>(
            existingNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
            StringComparer.CurrentCultureIgnoreCase);

        var year = currentYear;
        string name;
        do
        {
            name = string.Format(CultureInfo.CurrentCulture, format, year++);
        }
        while (taken.Contains(name));

        return name;
    }
}
