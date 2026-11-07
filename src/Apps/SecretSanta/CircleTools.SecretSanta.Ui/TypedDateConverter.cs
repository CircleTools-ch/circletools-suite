using System.Globalization;
using MudBlazor;
using MudBlazor.Utilities.Exceptions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Reads a date typed into a <see cref="MudDatePicker"/> more forgivingly than MudBlazor's default, which
/// only takes the exact short-date pattern of the formatting region: on a Swiss device <c>1.2.2026</c> was
/// refused, and on a British one <c>24.12.2026</c> (backlog PR-9).
/// </summary>
/// <remarks>
/// <para>
/// The region still decides the order of day and month (ADR-0056); only the writing is relaxed. <c>.</c>,
/// <c>-</c> and <c>/</c> all separate, day and month may have one digit, and the year two or four.
/// <c>03.04.2026</c> is 3 April in Switzerland and March 4 in the United States, as it is written there. A
/// date starting with a four-digit year is read as year, month, day everywhere: it cannot mean anything else.
/// </para>
/// <para>
/// A failure carries MudBlazor's own message key, so the picker shows it through
/// <c>SecretSantaMudLocalizer</c> in the UI language.
/// </para>
/// </remarks>
public sealed class TypedDateConverter : IReversibleConverter<DateTime?, string?>
{
    private const string InvalidDateKey = "Converter_InvalidDateTime";

    private static readonly char[] Separators = ['.', '-', '/'];

    /// <summary>The shared instance. It holds no state: the region is read on every conversion.</summary>
    public static TypedDateConverter Instance { get; } = new();

    /// <inheritdoc />
    public string? Convert(DateTime? input)
    {
        var culture = CultureInfo.CurrentCulture;
        return input?.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
    }

    /// <inheritdoc />
    public DateTime? ConvertBack(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return TryParse(input, CultureInfo.CurrentCulture, out var date)
            ? date
            : throw new ConversionException(InvalidDateKey);
    }

    /// <summary>Reads a typed date the way the formatting region orders it.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="region">The formatting region.</param>
    /// <param name="date">The date, when it could be read.</param>
    /// <returns>Whether <paramref name="text"/> is a real date.</returns>
    public static bool TryParse(string text, CultureInfo region, out DateTime date)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(region);

        // The region's own pattern first, so a region written some other way ("2026. 12. 24.") keeps working.
        var pattern = region.DateTimeFormat.ShortDatePattern;
        if (DateTime.TryParseExact(text.Trim(), pattern, region, DateTimeStyles.None, out date))
        {
            return true;
        }

        // RemoveEmptyEntries also forgives the trailing dot of "24.12.2026.".
        var parts = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !parts.All(part => part.All(char.IsAsciiDigit)))
        {
            return false;
        }

        // Written with four digits first it is ISO, and so always the Gregorian year whatever the region uses.
        if (parts[0].Length == 4)
        {
            return DateTime.TryParseExact(
                string.Join('/', parts), "yyyy'/'M'/'d", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        // Only the region's order remains, so a year that is not last has to be spelled out as above.
        var yearLast = pattern.IndexOf('y', StringComparison.Ordinal) > pattern.IndexOf('d', StringComparison.Ordinal);
        if (!yearLast || parts[2].Length is not (2 or 4))
        {
            return false;
        }

        var year = parts[2].Length == 2 ? "yy" : "yyyy";
        var dayFirst = pattern.IndexOf('d', StringComparison.Ordinal) < pattern.IndexOf('M', StringComparison.Ordinal);
        var format = dayFirst ? $"d'/'M'/'{year}" : $"M'/'d'/'{year}";
        return DateTime.TryParseExact(string.Join('/', parts), format, region, DateTimeStyles.None, out date);
    }
}
