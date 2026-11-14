// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// One Chromium for a test class, handing out separate contexts — each one a different person or device, with
/// its own storage. Every page it opens records console errors, so a test can assert a clean console.
/// </summary>
/// <remarks>
/// Contexts run the English UI on a wide viewport (the inline create field is hidden on phones), skip the one-time
/// storage onboarding, and capture what the app copies to the clipboard (<see cref="SharingLinks"/>).
/// </remarks>
internal sealed class E2EBrowser : IAsyncDisposable
{
    /// <summary>Init script that marks the one-time storage onboarding as already shown.</summary>
    private const string SkipStorageOnboardingScript =
        "try { localStorage.setItem('secretsanta.storage.onboarded','1'); } catch (e) {}";

    private const string EnglishScript = "try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}";

    private readonly List<string> _consoleIssues = [];
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Starts Chromium.</summary>
    /// <returns>The browser.</returns>
    public static async Task<E2EBrowser> LaunchAsync()
    {
        var browser = new E2EBrowser();
        browser._playwright = await Playwright.CreateAsync();
        browser._browser = await browser._playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        return browser;
    }

    /// <summary>Opens a page in a new context: a new person or device.</summary>
    /// <returns>The page.</returns>
    public async Task<IPage> NewPersonAsync()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });
        await context.AddInitScriptAsync(EnglishScript);
        await context.AddInitScriptAsync(SkipStorageOnboardingScript);
        await context.AddInitScriptAsync(SharingLinks.CaptureCopyScript);

        var page = await context.NewPageAsync();
        page.Console += (_, m) => { if (m.Type == "error" && IsRealFault(m.Text)) { _consoleIssues.Add($"[error] {m.Text}"); } };
        page.PageError += (_, e) => { if (IsRealFault(e)) { _consoleIssues.Add($"[pageerror] {e}"); } };
        return page;
    }

    /// <summary>Asserts that no page logged an application fault.</summary>
    public void AssertCleanConsole() =>
        Assert.True(_consoleIssues.Count == 0, "Console issues:\n" + string.Join("\n", _consoleIssues));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    // Not application faults: a missing favicon, and failed requests the app handles itself (an expected 401 on
    // a signed-out session check, a 404 for a link that no longer works).
    private static bool IsRealFault(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && !text.Contains("favicon", StringComparison.OrdinalIgnoreCase)
        && !text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase);
}
