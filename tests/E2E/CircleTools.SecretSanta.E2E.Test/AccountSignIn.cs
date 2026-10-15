using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Signs a browser into a Secret-Santa account the way a user does: register, follow the verification
/// link read out of band from Mailpit, then sign in with the password. A second browser context can sign
/// into the same account with <see cref="SignInAsync"/> to stand in for another device.
/// </summary>
internal static class AccountSignIn
{
    private static readonly HttpClient Mailpit = new() { BaseAddress = new Uri(SecretSantaStackFixture.MailpitApi) };

    /// <summary>Registers a fresh account, verifies its email and signs this page in.</summary>
    public static async Task RegisterVerifyAndSignInAsync(IPage page, string email, string password, string displayName)
    {
        await GotoAsync(page, "/account/register");
        await page.Locator("#register-name").FillAsync(displayName);
        await page.Locator("#register-email").FillAsync(email);
        await page.Locator("#register-password").FillAsync(password);
        await page.Locator("#register-password").PressAsync("Enter");

        var (userId, token) = await ReadVerificationLinkAsync(email);
        await GotoAsync(page, $"/verify-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

        await SignInAsync(page, email, password);
    }

    /// <summary>Signs this page into an existing, verified account.</summary>
    public static async Task SignInAsync(IPage page, string email, string password)
    {
        await GotoAsync(page, "/account/login");
        await page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await page.Locator("input[type=password]").First.FillAsync(password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        await page.WaitForURLAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageWaitForURLOptions { Timeout = 20_000 });
    }

    /// <summary>Navigates within the Secret-Santa app and waits for the network to settle.</summary>
    public static Task<IResponse?> GotoAsync(IPage page, string path) =>
        page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

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
