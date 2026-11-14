// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Identity.Tokens;

/// <summary>
/// Turns a User-Agent into the coarse device label shown in the list of signed-in devices (ADR-0079
/// amendment): the browser family (or the app) and the operating system — "Chrome · Windows", "Wichteln
/// app · Android". Versions, device models and anything else are left out on purpose (NFR-004). Pure.
/// </summary>
public static class DeviceLabel
{
    /// <summary>The User-Agent product token the suite's own apps send ("CircleTools-App/1.0 (Wichteln; Android)").</summary>
    public const string AppProduct = "CircleTools-App";

    /// <summary>Builds the label.</summary>
    /// <param name="userAgent">The request's User-Agent, if any.</param>
    /// <returns>The label, or an empty string when nothing is recognised.</returns>
    public static string FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return string.Empty;
        }

        var os = Os(userAgent);
        var client = App(userAgent) ?? Browser(userAgent);
        return (client, os) switch
        {
            (null, null) => string.Empty,
            (null, { } o) => o,
            ({ } c, null) => c,
            ({ } c, { } o) => $"{c} · {o}",
        };
    }

    // "CircleTools-App/0.1 (Wichteln; Android)" → "Wichteln app".
    private static string? App(string ua)
    {
        var at = ua.IndexOf(AppProduct, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }

        var open = ua.IndexOf('(', at);
        var semicolon = open < 0 ? -1 : ua.IndexOfAny([';', ')'], open);
        var name = open >= 0 && semicolon > open ? ua[(open + 1)..semicolon].Trim() : string.Empty;
        return string.IsNullOrEmpty(name) ? "App" : $"{name} app";
    }

    // Order matters: Edge and Opera also say "Chrome", Chrome also says "Safari".
    private static string? Browser(string ua) =>
        Has(ua, "Edg/") ? "Edge"
        : Has(ua, "OPR/") ? "Opera"
        : Has(ua, "Firefox/") || Has(ua, "FxiOS/") ? "Firefox"
        : Has(ua, "SamsungBrowser/") ? "Samsung Internet"
        : Has(ua, "Chrome/") || Has(ua, "CriOS/") ? "Chrome"
        : Has(ua, "Safari/") ? "Safari"
        : null;

    private static string? Os(string ua) =>
        Has(ua, "Android") ? "Android"
        : Has(ua, "iPhone") || Has(ua, "iPad") || Has(ua, "iOS") ? "iOS"
        : Has(ua, "Windows") ? "Windows"
        : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
        : Has(ua, "CrOS") ? "ChromeOS"
        : Has(ua, "Linux") ? "Linux"
        : null;

    private static bool Has(string ua, string token) => ua.Contains(token, StringComparison.OrdinalIgnoreCase);
}
