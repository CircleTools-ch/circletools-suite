using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Drives the participant's join journey end to end in Chromium against the live stack (FEAT-013,
/// ADR-0058): an organizer shares a group by link and adds members in one browser, then a second,
/// separate browser opens the join link, picks who they are, and lands in the group as that person —
/// all account-free.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class JoinFlowTests : IAsyncLifetime
{
    private readonly List<string> _consoleIssues = [];
    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Participant_joins_via_link_and_lands_in_the_group_as_the_chosen_person()
    {
        var joinUrl = await OrganizerSharesAndAddsMembersAsync();

        // A separate browser context — a different person, no shared storage — opens the join link.
        var participant = await NewContextAsync();
        var page = await participant.NewPageAsync();
        WireConsole(page);

        await page.GotoAsync(joinUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#join-panel").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // Both roster members are offered; pick "Anna" and keep "just this device".
        await page.GetByText("Bruno", new PageGetByTextOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.GetByText("Anna", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await page.ClickAsync("#join-continue");

        // Landed in the group as Anna.
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        await page.Locator("#joined-as").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        var banner = await page.Locator("#joined-as").InnerTextAsync();
        Assert.Contains("Anna", banner, StringComparison.Ordinal);

        Assert.True(_consoleIssues.Count == 0, "Console issues:\n" + string.Join("\n", _consoleIssues));
    }

    // The organizer, in their own browser: create a shared-by-link group, add two members, then read
    // the join link off the Sharing & sync screen.
    private async Task<string> OrganizerSharesAndAddsMembersAsync()
    {
        var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        WireConsole(page);

        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("#new-group-name", "E2E Wichteln");
        await page.ClickAsync("#mode-shared");
        await page.ClickAsync("#create-group-btn");
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });

        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(page.Url).Groups[1].Value;

        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/groups/{groupId}/members", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await AddMemberAsync(page, "Anna");
        await AddMemberAsync(page, "Bruno");

        // Give the background shared-sync a moment to push the roster to the server before the
        // participant fetches it via the join link.
        await page.WaitForTimeoutAsync(3_000);

        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/groups/{groupId}/sharing", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var url = await SharingLinks.CopyLinkAsync(page, "#join-link-card");
        Assert.Matches(@"https?://localhost:7081/join#t=\S+", url);
        return url;
    }

    private static async Task AddMemberAsync(IPage page, string name)
    {
        await page.FillAsync("#new-member-name", name);
        await page.ClickAsync("#add-member-btn");
        await page.GetByText(name, new PageGetByTextOptions { Exact = true }).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    private async Task<IBrowserContext> NewContextAsync()
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });
        // Force English so text-based selectors are stable (the app defaults to de-CH).
        await context.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");
        await context.AddInitScriptAsync(SharingLinks.CaptureCopyScript);
        return context;
    }

    private void WireConsole(IPage page)
    {
        page.Console += (_, m) => { if (m.Type == "error" && IsRealFault(m.Text)) { _consoleIssues.Add($"[error] {m.Text}"); } };
        page.PageError += (_, e) => { if (IsRealFault(e)) { _consoleIssues.Add($"[pageerror] {e}"); } };
    }

    private static bool IsRealFault(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && !text.Contains("favicon", StringComparison.OrdinalIgnoreCase)
        && !text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase);
}
