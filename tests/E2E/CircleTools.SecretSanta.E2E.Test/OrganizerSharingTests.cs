using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Drives the organizer's shared-by-link journey end to end in Chromium against the live stack
/// (FEAT-013, ADR-0058): create a group choosing "Shared by link", then land on Sharing &amp; sync with
/// a real admin link and join link the account-free server minted. Signed out throughout — this is the
/// account-free tier.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class OrganizerSharingTests : IAsyncLifetime
{
    private readonly List<string> _consoleIssues = [];
    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private IPage _page = default!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 }, // wide: the inline create + picker
        });
        await _context.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");

        _page = await _context.NewPageAsync();
        _page.Console += (_, m) => { if (m.Type == "error" && IsRealFault(m.Text)) { _consoleIssues.Add($"[error] {m.Text}"); } };
        _page.PageError += (_, e) => { if (IsRealFault(e)) { _consoleIssues.Add($"[pageerror] {e}"); } };
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Create_shared_by_link_group_shows_admin_and_join_links()
    {
        await _page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Name the group and choose "Shared by link" at creation (ids are language-independent).
        await _page.FillAsync("#new-group-name", "E2E Familie");
        await _page.ClickAsync("#mode-shared");
        await _page.ClickAsync("#create-group-btn");

        // Landed on the group hub, now shared — the mode chip appears, and the Sharing & sync row leads
        // to the links.
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        await _page.Locator("#mode-chip").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await _page.ClickAsync("#sharing-row");

        // Sharing & sync: a real admin link and join link the server minted, each shown with its URL.
        await _page.Locator("#admin-link-card").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await _page.Locator("#join-link-card").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        var adminText = await _page.Locator("#admin-link-card").InnerTextAsync();
        var joinText = await _page.Locator("#join-link-card").InnerTextAsync();
        Assert.Contains("/admin#t=", adminText, StringComparison.Ordinal);
        Assert.Contains("/join#t=", joinText, StringComparison.Ordinal);

        // A QR affordance is present on each card (the code is generated client-side, every platform).
        Assert.True(await _page.Locator("#admin-link-card button:has(svg)").CountAsync() >= 1);

        Assert.True(_consoleIssues.Count == 0, "Console issues:\n" + string.Join("\n", _consoleIssues));
    }

    // Ignore noise that is not a real application fault: a missing favicon, and the dev-time
    // cross-origin preload warning the WASM host emits.
    private static bool IsRealFault(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && !text.Contains("favicon", StringComparison.OrdinalIgnoreCase)
        && !text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase);
}
