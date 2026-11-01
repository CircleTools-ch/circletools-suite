using System.Globalization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The one order in which people are listed on screen (backlog LO-1, 2026-11-01): alphabetically by name,
/// the way the reader's language sorts — "Ännchen" next to "Anna" in German, accents ignored for the order in
/// French, and case never mattering. Lists used to follow the order members were added, which nobody could
/// see or change.
/// <para>
/// This is display only. The stored roster order, sync and the draw are untouched; pages sort what they show.
/// Ties (two people with the same name) keep their stored order, because the sort is stable.
/// </para>
/// </summary>
public static class DisplayOrder
{
    /// <summary>
    /// The name comparer for the current UI culture: linguistic, case-insensitive, and with accents and other
    /// marks deciding only between names that are otherwise equal.
    /// </summary>
    public static StringComparer Names => StringComparer.Create(CultureInfo.CurrentUICulture, CompareOptions.IgnoreCase);

    /// <summary>Orders <paramref name="items"/> by the name <paramref name="name"/> picks, for the current UI culture.</summary>
    /// <typeparam name="T">The listed item.</typeparam>
    /// <param name="items">The items, in stored order.</param>
    /// <param name="name">The display name of an item.</param>
    /// <returns>The items alphabetically by name; equal names keep their stored order.</returns>
    public static List<T> ByName<T>(IEnumerable<T> items, Func<T, string> name)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(name);
        return [.. items.OrderBy(name, Names)];
    }
}
