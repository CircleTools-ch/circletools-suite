using System.Text.Json;
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
    private static readonly HttpClient Mailpit = new() { BaseAddress = new Uri(SecretSantaStackFixture.MailpitApi) };

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
        await _page.FillAsync("#new-group-name", "E2E Claim");
        await _page.ClickAsync("#mode-shared");
        await _page.ClickAsync("#create-group-btn");
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

    private async Task RegisterVerifyAndSignInAsync(string email, string password, string displayName)
    {
        await Goto("/account/register");
        await _page.Locator("#register-name").FillAsync(displayName);
        await _page.Locator("#register-email").FillAsync(email);
        await _page.Locator("#register-password").FillAsync(password);
        await _page.Locator("#register-password").PressAsync("Enter");

        var (userId, token) = await ReadVerificationLinkAsync(email);
        await Goto($"/verify-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

        await Goto("/account/login");
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await _page.Locator("input[type=password]").First.FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        await _page.WaitForURLAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageWaitForURLOptions { Timeout = 20_000 });
    }

    private Task<IResponse?> Goto(string path) =>
        _page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private static async Task<(string UserId, string Token)> ReadVerificationLinkAsync(string email)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var list = await Mailpit.GetAsync("/api/v1/messages");
            using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            foreach (var message in listDoc.RootElement.GetProperty("messages").EnumerateArray())
            {
                var addressed = message.GetProperty("To").EnumerateArray()
                    .Any(to => string.Equals(to.GetProperty("Address").GetString(), email, StringComparison.OrdinalIgnoreCase));
                if (!addressed)
                {
                    continue;
                }

                var id = message.GetProperty("ID").GetString();
                using var detail = await Mailpit.GetAsync($"/api/v1/message/{id}");
                using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
                var body = (detailDoc.RootElement.TryGetProperty("HTML", out var html) ? html.GetString() : null)
                    + " " + (detailDoc.RootElement.TryGetProperty("Text", out var text) ? text.GetString() : null);

                var userId = Regex.Match(body ?? string.Empty, @"userId=([^&""'\s]+)");
                var token = Regex.Match(body ?? string.Empty, @"token=([^&""'\s]+)");
                if (userId.Success && token.Success)
                {
                    return (Uri.UnescapeDataString(userId.Groups[1].Value), Uri.UnescapeDataString(token.Groups[1].Value));
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new InvalidOperationException($"No verification mail arrived for {email}.");
    }
}
