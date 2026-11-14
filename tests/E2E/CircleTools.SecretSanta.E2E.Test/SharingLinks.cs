// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Reads a share link off the Sharing &amp; sync screen. Since ADR-0068 / FEAT-020 the raw URL is no
/// longer rendered as text — it lives behind the Copy / QR / Share actions — so the tests obtain it the
/// way a user does: click <em>Copy</em> and read what the app copied.
/// </summary>
/// <remarks>
/// Headless Chromium's real clipboard is focus/permission sensitive and flaky, so instead of reading the
/// OS clipboard we hook <c>navigator.clipboard.writeText</c> (which the Copy button calls) and capture its
/// argument on <c>window.__copiedText</c>. This still drives the real button and asserts on the exact URL
/// the app hands to the clipboard. Contexts that use this must add <see cref="CaptureCopyScript"/> as an
/// init script before navigating.
/// </remarks>
internal static class SharingLinks
{
    /// <summary>Init script that records the last value the app copied on <c>window.__copiedText</c>.</summary>
    public const string CaptureCopyScript =
        """
        (() => {
          try {
            window.__copiedText = null;
            if (navigator.clipboard) {
              const orig = navigator.clipboard.writeText
                ? navigator.clipboard.writeText.bind(navigator.clipboard)
                : null;
              navigator.clipboard.writeText = (text) => {
                window.__copiedText = text;
                // Fire the real write but never let its (sync or async) failure surface: the app treats
                // a rejection as "copy failed" and would not flip the button to "Copied".
                try { if (orig) { orig(text).catch(() => {}); } } catch (e) { /* ignore */ }
                return Promise.resolve();
              };
            }
          } catch (e) { /* leave the app's clipboard untouched on any failure */ }
        })();
        """;

    /// <summary>Clicks the Copy button on the given link card and returns the URL the app copied.</summary>
    public static Task<string> CopyLinkAsync(IPage page, string cardSelector) =>
        CopyLinkAsync(page, page.Locator(cardSelector), cardSelector);

    /// <summary>Clicks the Copy button on the reveal-link card for <paramref name="member"/> on the share page.</summary>
    public static Task<string> CopyRevealLinkAsync(IPage page, string member) =>
        CopyLinkAsync(page, page.Locator(".mud-card").Filter(new LocatorFilterOptions { HasText = $"For: {member}" }), member);

    private static async Task<string> CopyLinkAsync(IPage page, ILocator card, string what)
    {
        await card.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await page.EvaluateAsync("() => { window.__copiedText = null; }");

        await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Copy", Exact = true }).ClickAsync();

        // Wait for the app to have called clipboard.writeText (captured by CaptureCopyScript) rather than
        // on the button's localized "Copied!" label, so the test stays independent of the wording.
        await page.WaitForFunctionAsync("() => !!window.__copiedText", null, new PageWaitForFunctionOptions { Timeout = 5_000 });

        var url = await page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.False(string.IsNullOrEmpty(url), $"Could not read the link copied from '{what}'.");
        return url;
    }
}
