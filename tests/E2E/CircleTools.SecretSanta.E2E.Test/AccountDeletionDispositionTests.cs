using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Drives the account-deletion disposition end to end in Chromium against the live stack (FEAT-013,
/// FR-043, ADR-0058): an account that owns a shared-by-link group is shown the "what happens to these
/// groups" choice on the delete screen, and deletion carries the chosen disposition through.
/// </summary>
/// <remarks>
/// The owned group is created in the Secret-Santa app (create → Sharing &amp; sync → claim admin),
/// which is the only way an account comes to own a shared-by-link group. The delete screen itself lives
/// in the Main shell (main-web), a different origin — so the account signs in again there.
/// </remarks>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class AccountDeletionDispositionTests : IAsyncLifetime
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
        // Pin both apps to English so the assertions are stable across the two origins.
        await _context.AddInitScriptAsync(
            "try { localStorage.setItem('secretsanta.culture','en'); localStorage.setItem('main.culture','en'); } catch (e) {}");
        await _context.AddInitScriptAsync(GroupCreation.SkipStorageOnboardingScript);
        _page = await _context.NewPageAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Owner_deleting_their_account_is_shown_a_disposition_for_their_shared_groups()
    {
        var email = $"deletedisp.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";

        // Register + verify + sign in on the Secret-Santa app, then create an account-owned shared group.
        await SignUpVerifyAndSignInAsync(SecretSantaStackFixture.WebOrigin, email, password, "Delete Tester");
        await Goto(SecretSantaStackFixture.WebOrigin, "/");
        await GroupCreation.CreateSharedGroupAsync(_page, "E2E Delete Disposition");
        await _page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var groupId = new Regex(@"/groups/([0-9a-fA-F-]+)$").Match(_page.Url).Groups[1].Value;

        // Signed in, the shared group is account-owned at once (ADR-0081).
        await Goto(SecretSantaStackFixture.WebOrigin, $"/groups/{groupId}/sharing");
        await _page.Locator("#account-owned").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // Move to the Main shell (different origin) and sign in again with the same account.
        await SignInAsync(SecretSantaStackFixture.MainWebOrigin, email, password);
        await Goto(SecretSantaStackFixture.MainWebOrigin, "/account/data");

        // The disposition appears because the account owns a shared-by-link group.
        var disposition = _page.Locator("#delete-shared-disposition");
        await disposition.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        Assert.Equal(2, await disposition.Locator(".mud-radio").CountAsync()); // hand off / delete too

        // Choose "delete these online copies too", confirm with the password, and delete.
        await disposition.Locator(".mud-radio").Nth(1).ClickAsync();
        await _page.Locator("#delete-password").FillAsync(password);

        var deleteResponse = _page.WaitForResponseAsync(r => r.Url.Contains("/account/delete", StringComparison.Ordinal));
        await _page.Locator(".circletools-danger form button[type=submit]").ClickAsync();
        Assert.Equal(204, (await deleteResponse).Status);

        // The account is gone.
        await _page.GetByText(new Regex("gone", RegexOptions.IgnoreCase))
            .First.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // And the address no longer signs in — deletion really happened.
        await Goto(SecretSantaStackFixture.MainWebOrigin, "/account/login");
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await _page.Locator("input[type=password]").First.FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        // Still on the login page after a moment: the sign-in was refused.
        await _page.WaitForTimeoutAsync(2_000);
        Assert.Contains("/account/login", _page.Url, StringComparison.Ordinal);
    }

    private async Task SignUpVerifyAndSignInAsync(string origin, string email, string password, string displayName)
    {
        await Goto(origin, "/account/register");
        await _page.Locator("#register-name").FillAsync(displayName);
        await _page.Locator("#register-email").FillAsync(email);
        await _page.Locator("#register-password").FillAsync(password);
        await _page.Locator("#register-password").PressAsync("Enter");

        var (userId, token) = await ReadVerificationLinkAsync(email);
        await Goto(origin, $"/verify-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

        await SignInAsync(origin, email, password);
    }

    private async Task SignInAsync(string origin, string email, string password)
    {
        await Goto(origin, "/account/login");
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await _page.Locator("input[type=password]").First.FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        await _page.WaitForURLAsync($"{origin}/", new PageWaitForURLOptions { Timeout = 20_000 });
    }

    private Task<IResponse?> Goto(string origin, string path) =>
        _page.GotoAsync($"{origin}{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

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
