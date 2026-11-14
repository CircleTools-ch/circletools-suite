// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// One wishlist line read for display (ADR-0083): its text and the first web link written anywhere in it —
/// "The blue one https://shop.example/blue". Nothing special has to be typed; the stored item stays the plain
/// line, and this only reads it. Pure.
/// </summary>
/// <param name="Text">The line without its link; empty when the line is only a link.</param>
/// <param name="Link">The first http(s) link in the line, if any.</param>
public sealed record WishlistItem(string Text, Uri? Link)
{
    // A static Regex rather than [GeneratedRegex]: the docs build compiles without source generators.
    private static readonly Regex UrlInText = new(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Reads a stored wishlist line.</summary>
    /// <param name="line">The line as stored.</param>
    /// <returns>The item's text and link.</returns>
    public static WishlistItem Parse(string? line)
    {
        var raw = (line ?? string.Empty).Trim();
        foreach (Match match in UrlInText.Matches(raw))
        {
            if (TryLink(match.Value, out var link))
            {
                var text = string.Join(' ', raw.Remove(match.Index, match.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Trim(' ', '-', '–', ':', '·', '|', ',');
                return new WishlistItem(text, link);
            }
        }

        return new WishlistItem(raw, null);
    }

    private static bool TryLink(string value, out Uri? uri)
    {
        uri = null;
        var candidate = value.TrimEnd('.', ',', ')', ';', '!', '?');
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(parsed.Host))
        {
            uri = parsed;
            return true;
        }

        return false;
    }
}
