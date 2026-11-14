// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="IcsBuilder"/> (MNF-6, FR-044): a standards-shaped all-day VEVENT, correct
/// escaping and line folding, the deep link in both URL and DESCRIPTION, and a date that does not shift
/// with the machine's time zone.
/// </summary>
public sealed class IcsBuilderTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 9, 15, 8, 30, 0, TimeSpan.Zero);

    private static Dictionary<string, string> Properties(string ics)
    {
        // Unfold (RFC 5545: a CRLF followed by a space continues the previous line), then split PROP:VALUE.
        var unfolded = ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in unfolded.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                map[line[..colon]] = line[(colon + 1)..];
            }
        }

        return map;
    }

    [Fact]
    public void Builds_AnAllDayVevent_WithDtEndTheNextDay()
    {
        var ics = IcsBuilder.Build(
            new CalendarEvent("uid-1", "Christmas 2026", new DateOnly(2026, 12, 24)), Stamp);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VEVENT\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);

        var props = Properties(ics);
        Assert.Equal("20261224", props["DTSTART;VALUE=DATE"]);
        Assert.Equal("20261225", props["DTEND;VALUE=DATE"]);
        Assert.Equal("Christmas 2026", props["SUMMARY"]);
        Assert.Equal("20260915T083000Z", props["DTSTAMP"]);
        Assert.Equal("uid-1", props["UID"]);
    }

    [Fact]
    public void Date_DoesNotShift_WithTheLocalTimeZone()
    {
        // The DateOnly is formatted directly, so no UTC conversion can move it across midnight.
        var ics = IcsBuilder.Build(
            new CalendarEvent("uid", "Party", new DateOnly(2026, 1, 1)), Stamp);

        Assert.Contains("DTSTART;VALUE=DATE:20260101\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void OmitsLocationAndDescription_WhenEmpty()
    {
        var ics = IcsBuilder.Build(new CalendarEvent("uid", "Bare", new DateOnly(2026, 12, 24)), Stamp);

        Assert.DoesNotContain("LOCATION:", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("DESCRIPTION:", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("URL:", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void PutsTheLink_InBothUrlAndDescription()
    {
        const string url = "https://secretsanta.circletools.ch/groups/g1/events/e1";
        var ics = IcsBuilder.Build(
            new CalendarEvent("uid", "X", new DateOnly(2026, 12, 24), Notes: "Bring a gift", Url: url), Stamp);

        var props = Properties(ics);
        Assert.Equal(url, props["URL"]);
        Assert.Contains("Bring a gift", props["DESCRIPTION"], StringComparison.Ordinal);
        Assert.Contains(url, props["DESCRIPTION"], StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_SpecialCharacters()
    {
        var ics = IcsBuilder.Build(
            new CalendarEvent("uid", "Gift, wrap; done\\", new DateOnly(2026, 12, 24), Notes: "line1\nline2"),
            Stamp);

        var props = Properties(ics);
        Assert.Equal("Gift\\, wrap\\; done\\\\", props["SUMMARY"]);
        Assert.Equal("line1\\nline2", props["DESCRIPTION"]);
    }

    [Fact]
    public void Folds_LongLines_ToSeventyFiveOctets()
    {
        var longTitle = new string('A', 200);
        var ics = IcsBuilder.Build(new CalendarEvent("uid", longTitle, new DateOnly(2026, 12, 24)), Stamp);

        foreach (var line in ics.Split("\r\n"))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"Line exceeds 75 octets: {line.Length}");
        }

        // Unfolding restores the full summary, so folding is lossless.
        Assert.Equal(longTitle, Properties(ics)["SUMMARY"]);
    }

    [Fact]
    public void Folds_WithoutSplitting_MultiByteCharacters()
    {
        // A run of 3-byte characters (…) forces folds mid-run; unfolding must restore every character.
        var title = string.Concat(Enumerable.Repeat("Zürich…", 20));
        var ics = IcsBuilder.Build(new CalendarEvent("uid", title, new DateOnly(2026, 12, 24)), Stamp);

        foreach (var line in ics.Split("\r\n"))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75);
        }

        Assert.Equal(title, Properties(ics)["SUMMARY"]);
    }
}
