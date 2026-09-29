using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Drives the admin recovery path end to end in Chromium (FEAT-013, ADR-0058): a participant joins as
/// a member (locking it), the organizer frees that spot from the Members screen, and a different
/// participant can then join as the same member — the sole recovery mechanism for a lost/changed
/// device.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class AdminReleaseTests : IAsyncLifetime
{
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
    public async Task Admin_frees_a_claimed_spot_so_it_can_be_claimed_again()
    {
        // Organizer: create a shared group, add Anna, read the join link. Keep the page for the release.
        var organizer = await NewContextAsync();
        var page = await organizer.NewPageAsync();
        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("#new-group-name", "E2E Release");
        await page.ClickAsync("#mode-shared");
        await page.ClickAsync("#create-group-btn");
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(page.Url).Groups[1].Value;

        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/groups/{groupId}/members", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("#new-member-name", "Anna");
        await page.ClickAsync("#add-member-btn");
        await page.GetByText("Anna", new PageGetByTextOptions { Exact = true }).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(3_000); // let the roster push to the server

        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/groups/{groupId}/sharing", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var joinUrl = await SharingLinks.CopyLinkAsync(page, "#join-link-card");

        // First participant joins as Anna — the spot is now locked.
        await JoinAsAsync(joinUrl, "Anna");

        // Organizer frees Anna's spot from the Members screen.
        await page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}/groups/{groupId}/members", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var annaCard = page.Locator(".mud-card").Filter(new LocatorFilterOptions { HasTextString = "Anna" });
        await annaCard.GetByText("Joined", new LocatorGetByTextOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await annaCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "More actions" }).ClickAsync();
        await page.GetByText("Free this spot", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Free the spot" }).ClickAsync();

        // A different participant can now claim Anna again (recovery works).
        var joinedAgain = await JoinAsAsync(joinUrl, "Anna");
        Assert.Contains("Anna", joinedAgain, StringComparison.Ordinal);
    }

    // Opens the join link in a fresh browser context and joins as the named member; returns the
    // "you joined as" banner text.
    private async Task<string> JoinAsAsync(string joinUrl, string member)
    {
        var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(joinUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#join-panel").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await page.GetByText(member, new PageGetByTextOptions { Exact = true }).ClickAsync();
        await page.ClickAsync("#join-continue");
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        await page.Locator("#joined-as").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        return await page.Locator("#joined-as").InnerTextAsync();
    }

    private async Task<IBrowserContext> NewContextAsync()
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });
        await context.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");
        await context.AddInitScriptAsync(SharingLinks.CaptureCopyScript);
        return context;
    }
}
