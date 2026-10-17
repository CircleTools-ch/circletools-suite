using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Deleting a "My account" group reaches the account's other devices (ADR-0078): two browser contexts
/// signed into the same account stand in for two devices. Covers both kinds of "My account" group — a
/// shared-by-link group whose admin was claimed into the account (seen on a Pixel, where the deleted
/// group stayed for good) and a group created as "My account" while offline.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class MultiDeviceDeleteTests : IAsyncLifetime
{
    private const string Password = "Correct-Horse-9";
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(45);

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
    public async Task An_admin_claimed_group_deleted_on_one_device_disappears_on_the_other()
    {
        const string name = "E2E Two Devices Claimed";
        var email = $"twodevices.claimed+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";

        var (deviceA, contextA) = await NewDeviceAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(deviceA, email, Password, "Two Devices");
        await AccountSignIn.GotoAsync(deviceA, "/");
        await GroupCreation.CreateSharedGroupAsync(deviceA, name);
        var groupId = await WaitForGroupPageAsync(deviceA);
        // Signed in, the shared group is account-owned at once (ADR-0081).
        await AccountSignIn.GotoAsync(deviceA, $"/groups/{groupId}/sharing");
        await deviceA.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // Device B signs into the same account and discovers the group.
        var (deviceB, contextB) = await NewDeviceAsync();
        await AccountSignIn.SignInAsync(deviceB, email, Password);
        await WaitForGroupListedAsync(deviceB, name, listed: true);

        await DeleteFromListAsync(deviceA, name);

        // Before the fix, B kept its copy for good: the owner read answered 404 and B left it as it was.
        await WaitForGroupListedAsync(deviceB, name, listed: false);

        await contextA.CloseAsync();
        await contextB.CloseAsync();
    }

    [Fact]
    public async Task A_group_created_offline_as_my_account_and_deleted_on_another_device_disappears()
    {
        const string name = "E2E Two Devices Offline";
        var email = $"twodevices.offline+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";

        var (deviceA, contextA) = await NewDeviceAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(deviceA, email, Password, "Offline Device");
        await AccountSignIn.GotoAsync(deviceA, "/");

        // Created as "My account" while offline: marked for the account, uploaded once back online.
        await contextA.SetOfflineAsync(true);
        await GroupCreation.CreateGroupAsync(deviceA, name, "My account");
        await WaitForGroupPageAsync(deviceA);
        await contextA.SetOfflineAsync(false);

        var (deviceB, contextB) = await NewDeviceAsync();
        await AccountSignIn.SignInAsync(deviceB, email, Password);
        await WaitForGroupListedAsync(deviceB, name, listed: true, reload: async () =>
        {
            await AccountSignIn.GotoAsync(deviceA, "/"); // let device A's cycle upload it
            await AccountSignIn.GotoAsync(deviceB, "/");
        });

        await DeleteFromListAsync(deviceB, name);

        await WaitForGroupListedAsync(deviceA, name, listed: false);

        await contextA.CloseAsync();
        await contextB.CloseAsync();
    }

    private async Task<(IPage Page, IBrowserContext Context)> NewDeviceAsync()
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });
        await context.AddInitScriptAsync("try { localStorage.setItem('secretsanta.culture','en'); } catch (e) {}");
        await context.AddInitScriptAsync(GroupCreation.SkipStorageOnboardingScript);
        return (await context.NewPageAsync(), context);
    }

    private static async Task<string> WaitForGroupPageAsync(IPage page)
    {
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        return new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(page.Url).Groups[1].Value;
    }

    private static async Task DeleteFromListAsync(IPage page, string name)
    {
        await AccountSignIn.GotoAsync(page, "/");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = $"More actions for {name}" }).ClickAsync();
        await page.GetByText("Delete", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Yes, delete" }).ClickAsync();
        await page.GetByText(name, new PageGetByTextOptions { Exact = true }).WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10_000 });
        await page.WaitForTimeoutAsync(3_000); // let the delete reach the server
    }

    // Reloads the group list (each load starts a sync cycle) until the group is, or is no longer, listed.
    private static async Task WaitForGroupListedAsync(IPage page, string name, bool listed, Func<Task>? reload = null)
    {
        var deadline = DateTime.UtcNow + SyncTimeout;
        while (true)
        {
            if (reload is null)
            {
                await AccountSignIn.GotoAsync(page, "/");
            }
            else
            {
                await reload();
            }

            await page.WaitForTimeoutAsync(3_000);
            var count = await page.GetByText(name, new PageGetByTextOptions { Exact = true }).CountAsync();
            if ((count > 0) == listed)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"\"{name}\" was {(listed ? "never listed" : "still listed")} after {SyncTimeout.TotalSeconds:N0}s.");
            }
        }
    }
}
