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
    public async Task Signed_in_organizers_shared_group_is_account_owned_from_the_start()
    {
        var email = $"claimadmin.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";

        await RegisterVerifyAndSignInAsync(email, password, "Claim Tester");

        // Signed in, sharing shares and claims admin in one step (ADR-0081): "My account · shared".
        await Goto("/");
        await GroupCreation.CreateSharedGroupAsync(_page, "E2E Claim");
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(_page.Url).Groups[1].Value;

        // Owned by the account, with a join link to invite people and no admin link to pass around.
        await Goto($"/groups/{groupId}/sharing");
        await _page.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await _page.Locator("#join-link-card").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        Assert.Equal(0, await _page.Locator("#admin-link-card").CountAsync());
    }

    [Fact]
    public async Task A_link_group_made_signed_out_is_kept_in_the_account_after_sign_in()
    {
        var email = $"keepinaccount.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";

        // Signed out, "Shared by link" has no owner: only its admin link manages it.
        await Goto("/");
        await GroupCreation.CreateSharedGroupAsync(_page, "E2E Keep");
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(_page.Url).Groups[1].Value;

        await RegisterVerifyAndSignInAsync(email, password, "Keep Tester");
        await DismissImportPromptAsync();

        // Signed in, the sharing page offers to keep it in the account — claiming admin (ADR-0081).
        await Goto($"/groups/{groupId}/sharing");
        var keep = _page.Locator("#keep-in-account");
        await keep.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await keep.GetByRole(AriaRole.Button).ClickAsync();

        // It asks first: the management link stops working, the join link keeps working.
        await _page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Keep in my account" }).ClickAsync();

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

        // Signed in, the shared group is account-owned at once (ADR-0081). Keep its join link: it must stop
        // working once the group is deleted.
        await Goto($"/groups/{groupId}/sharing");
        await _page.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        var joinUrl = await SharingLinks.CopyLinkAsync(_page, "#join-link-card");

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

    // Signing in with a group made while signed out may ask whether to bring it into the account (FEAT-025).
    // "Later" decides nothing, so the test makes the choice itself on the sharing page.
    private async Task DismissImportPromptAsync()
    {
        var later = _page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Later" });
        try
        {
            await later.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
            await later.ClickAsync();
        }
        catch (TimeoutException)
        {
            // No prompt for this group — nothing to dismiss.
        }
    }

    private Task RegisterVerifyAndSignInAsync(string email, string password, string displayName) =>
        AccountSignIn.RegisterVerifyAndSignInAsync(_page, email, password, displayName);

    private Task<IResponse?> Goto(string path) => AccountSignIn.GotoAsync(_page, path);
}
