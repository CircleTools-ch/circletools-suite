using System.Globalization;
using System.Text;

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Builds a minimal, standards-compliant iCalendar (<c>.ics</c>) document for a single all-day
/// <see cref="CalendarEvent"/> (RFC 5545). Head-agnostic and dependency-free, so the same entry is
/// produced in the WebAssembly web head and the MAUI heads (MNF-6, FR-044). This is text building, not a
/// calendar client.
/// </summary>
public static class IcsBuilder
{
    private const string ProductId = "-//CircleTools//Secret-Santa//EN";

    /// <summary>Builds the <c>.ics</c> document text for one occasion.</summary>
    /// <param name="calendarEvent">The occasion to render as an all-day VEVENT.</param>
    /// <param name="now">The current instant, stamped as <c>DTSTAMP</c>. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <returns>The complete <c>.ics</c> document, CRLF-delimited.</returns>
    public static string Build(CalendarEvent calendarEvent, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        // An all-day event spans [Date, Date+1) with DATE-valued (time-less) bounds, per RFC 5545.
        var start = calendarEvent.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var end = calendarEvent.Date.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var stamp = (now ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var description = BuildDescription(calendarEvent);

        var builder = new StringBuilder();
        AppendLine(builder, "BEGIN:VCALENDAR");
        AppendLine(builder, "VERSION:2.0");
        AppendLine(builder, $"PRODID:{ProductId}");
        AppendLine(builder, "CALSCALE:GREGORIAN");
        AppendLine(builder, "METHOD:PUBLISH");
        AppendLine(builder, "BEGIN:VEVENT");
        AppendLine(builder, $"UID:{Escape(calendarEvent.Uid)}");
        AppendLine(builder, $"DTSTAMP:{stamp}");
        AppendLine(builder, $"DTSTART;VALUE=DATE:{start}");
        AppendLine(builder, $"DTEND;VALUE=DATE:{end}");
        AppendLine(builder, $"SUMMARY:{Escape(calendarEvent.Title)}");
        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            AppendLine(builder, $"LOCATION:{Escape(calendarEvent.Location)}");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            AppendLine(builder, $"DESCRIPTION:{Escape(description)}");
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.Url))
        {
            AppendLine(builder, $"URL:{Escape(calendarEvent.Url)}");
        }

        AppendLine(builder, "END:VEVENT");
        AppendLine(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    // Notes first, then the link on its own line so a calendar that hides the URL property still shows it.
    private static string? BuildDescription(CalendarEvent calendarEvent)
    {
        var hasNotes = !string.IsNullOrWhiteSpace(calendarEvent.Notes);
        var hasUrl = !string.IsNullOrWhiteSpace(calendarEvent.Url);
        return (hasNotes, hasUrl) switch
        {
            (true, true) => $"{calendarEvent.Notes}\n{calendarEvent.Url}",
            (true, false) => calendarEvent.Notes,
            (false, true) => calendarEvent.Url,
            _ => null,
        };
    }

    // RFC 5545 §3.3.11: backslash, semicolon and comma are escaped, and newlines become the literal \n.
    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\n", StringComparison.Ordinal);

    // RFC 5545 §3.1: content lines are folded at 75 octets by inserting CRLF + a single space. Folding on
    // octets (UTF-8 bytes), not chars, keeps multi-byte content within the limit.
    private static void AppendLine(StringBuilder builder, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= 75)
        {
            builder.Append(line).Append("\r\n");
            return;
        }

        var written = 0;
        var first = true;
        while (written < bytes.Length)
        {
            // 75 octets on the first line, 74 on continuations (the leading space counts toward the limit).
            var budget = first ? 75 : 74;
            var take = Math.Min(budget, bytes.Length - written);

            // Do not split a multi-byte UTF-8 sequence: back off to a code-point boundary. The bounds
            // check comes first so the byte is only read when a next byte exists (&& short-circuits).
            while (take > 0 && written + take < bytes.Length && (bytes[written + take] & 0xC0) == 0x80)
            {
                take--;
            }

            if (!first)
            {
                builder.Append(' ');
            }

            builder.Append(Encoding.UTF8.GetString(bytes, written, take)).Append("\r\n");
            written += take;
            first = false;
        }
    }
}
