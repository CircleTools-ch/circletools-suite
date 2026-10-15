using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Drives claim-admin end to end in Chromium against the live stack (FEAT-013, ADR-0058): a signed-in
/// organizer shares a group by link, then claims admin into their account — the group becomes
/// account-owned and the admin link is retired (My account mode).
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class ClaimAdminTests : IAsyncLifetime
{
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
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });
        await _context.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");
        await _context.AddInitScriptAsync(GroupCreation.SkipStorageOnboardingScript);
        await _context.AddInitScriptAsync(SharingLinks.CaptureCopyScript);
        _page = await _context.NewPageAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Signed_in_organizer_claims_admin_and_the_group_becomes_account_owned()
    {
        var email = $"claimadmin.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";

        await RegisterVerifyAndSignInAsync(email, password, "Claim Tester");

        // Create a shared-by-link group while signed in.
        await Goto("/");
        await GroupCreation.CreateSharedGroupAsync(_page, "E2E Claim");
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(_page.Url).Groups[1].Value;

        // Claim admin into the account from Sharing & sync (My account mode).
        await Goto($"/groups/{groupId}/sharing");
        await _page.Locator("#admin-link-card").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await _page.ClickAsync("#mode-account");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Claim admin" }).ClickAsync();

        // The group is now account-owned; the admin/join link cards are gone (My account mode).
        await _page.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        Assert.Equal(0, await _page.Locator("#admin-link-card").CountAsync());
    }

    [Fact]
    public async Task Deleting_an_admin_claimed_group_removes_it_online_and_it_does_not_come_back()
    {
        var email = $"deleteowned.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";
        const string name = "E2E Delete Owned";

        await RegisterVerifyAndSignInAsync(email, password, "Delete Owned Tester");

        await Goto("/");
        await GroupCreation.CreateSharedGroupAsync(_page, name);
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(_page.Url).Groups[1].Value;

        // Keep the join link: claiming admin hides it, and it must stop working once the group is deleted.
        await Goto($"/groups/{groupId}/sharing");
        var joinUrl = await SharingLinks.CopyLinkAsync(_page, "#join-link-card");
        await _page.ClickAsync("#mode-account");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Claim admin" }).ClickAsync();
        await _page.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // Delete it from the group list (ADR-0078 amendment).
        await Goto("/");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = $"More actions for {name}" }).ClickAsync();
        await _page.GetByText("Delete", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Yes, delete" }).ClickAsync();
        await _page.GetByText(name, new PageGetByTextOptions { Exact = true }).WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10_000 });

        // Let sync cycles run (each reload starts one). Before the fix, owned-group discovery brought it back.
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await _page.WaitForTimeoutAsync(4_000);
            await Goto("/");
        }

        await _page.WaitForTimeoutAsync(4_000);
        Assert.Equal(0, await _page.GetByText(name, new PageGetByTextOptions { Exact = true }).CountAsync());

        // The online copy is gone too, so the join link no longer finds the group.
        var participant = await _browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        await participant.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");
        var participantPage = await participant.NewPageAsync();
        await participantPage.GotoAsync(joinUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await participantPage.Locator("#join-notfound").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await participant.CloseAsync();
    }

    private Task RegisterVerifyAndSignInAsync(string email, string password, string displayName) =>
        AccountSignIn.RegisterVerifyAndSignInAsync(_page, email, password, displayName);

    private Task<IResponse?> Goto(string path) => AccountSignIn.GotoAsync(_page, path);
}
