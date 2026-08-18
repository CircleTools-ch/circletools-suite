using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CircleTools.Main.Contracts;
using Microsoft.Playwright;

namespace CircleTools.WebAuthn.E2E.Test;

internal static class Json
{
    /// <summary>Shared web-defaults options (camelCase) for the passkey-list deserialization.</summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Drives the real WebAuthn ceremony end to end in Chromium via a CDP virtual authenticator
/// (arc42 D4, ADR-0045): register a passkey (FR-010), sign in with it (FR-011), and confirm the
/// stored credential holds only public material (NFR-004). This is the round-trip that no
/// in-process test can exercise — <c>WebApplicationFactory</c> has no authenticator.
/// </summary>
/// <remarks>
/// The RP ID resolves to <c>localhost</c> in Development (<c>IdentityPasskeyOptions.ServerDomain</c>
/// is left unset), so the virtual authenticator, created with RP-relative discoverable resident
/// keys, satisfies both the attestation and the <c>user: null</c> discoverable assertion the app's
/// own passkey sign-in uses.
/// </remarks>
[Collection(WebAuthnE2E.CollectionName)]
public sealed class PasskeyCeremonyTests : IAsyncLifetime
{
    private static readonly HttpClient Mailpit = new() { BaseAddress = new Uri(WebAuthnStackFixture.MailpitApi) };

    private readonly WebAuthnStackFixture _stack;
    private readonly List<string> _consoleIssues = [];

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private IPage _page = default!;

    public PasskeyCeremonyTests(WebAuthnStackFixture stack) => _stack = stack;

    async Task IAsyncLifetime.InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 950 },
        });

        _page = await _context.NewPageAsync();
        _page.Console += (_, message) =>
        {
            // Genuine app faults surface as an uncaught exception (PageError, below) or an explicit
            // console.error with a message. Chromium also logs every *failed subresource* as a console
            // "error" with the generic, detail-free text "Failed to load resource: ..." — a favicon, a
            // deliberately-anonymous 401 probe, or a cold-boot fetch race — which is network noise, not
            // an application error, and is flaky by nature. Exclude only that; keep everything else.
            if (message.Type == "error"
                && !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal))
            {
                _consoleIssues.Add($"[error] {message.Text}");
            }
        };
        _page.PageError += (_, error) => _consoleIssues.Add($"[pageerror] {error}");

        // The virtual authenticator: an internal (platform) CTAP2 device that supports resident keys
        // and user verification, and answers presence/UV automatically — so the ceremony runs with no
        // human at the keyboard. It lives on the context, so the credential created during registration
        // is still there for the discoverable sign-in later.
        var cdp = await _context.NewCDPSessionAsync(_page);
        await cdp.SendAsync("WebAuthn.enable");
        await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["automaticPresenceSimulation"] = true,
                ["isUserVerified"] = true,
            },
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Passkey_registers_then_signs_in_and_stores_only_public_material()
    {
        var email = $"passkey.e2e+{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@circletools.test";
        const string password = "Correct-Horse-9";
        const string displayName = "Passkey Tester";

        await GotoAsync("/");
        await SetEnglishAsync();

        // Reach an authenticated session the ordinary way (the passkey is added to an existing account).
        await RegisterAndVerifyAsync(email, password, displayName);
        await PasswordSignInAsync(email, password);

        // FR-010 — register a passkey through the real create() ceremony.
        await GotoAsync("/account/security");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a passkey" }).ClickAsync();

        // Attestation succeeded and a credential is now stored: the Security roster lists it, showing
        // only public metadata (a label and the date) — never key material.
        await _page.GetByText(new Regex("Added on")).First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 20_000 });

        // FR-011 — sign back in with the passkey through the real get() ceremony (discoverable).
        await SignOutAsync();
        await GotoAsync("/account/login");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in with a passkey" }).ClickAsync();

        // Landed authenticated: the account menu now offers "Sign out".
        await OpenAccountMenuAsync();
        await _page.GetByText("Sign out").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // NFR-004 — inspect the stored credential server-side: exactly one passkey, exposed as public
        // metadata only. The private key never left the authenticator (WebAuthn), and the API surface
        // returns no key bytes at all. That a genuine assertion just verified against it proves the
        // stored public key is real, not a placeholder.
        var (passkeys, rawJson) = await ListStoredPasskeysAsync(email, password);
        Assert.Single(passkeys);
        Assert.False(string.IsNullOrWhiteSpace(passkeys[0].CredentialId));
        Assert.DoesNotContain("privateKey", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publicKey", rawJson, StringComparison.OrdinalIgnoreCase);

        Assert.True(_consoleIssues.Count == 0, "Console issues:\n" + string.Join("\n", _consoleIssues));
    }

    // ---- browser helpers -------------------------------------------------------------------------

    private Task<IResponse?> GotoAsync(string path) =>
        _page.GotoAsync($"{WebAuthnStackFixture.WebOrigin}{path}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private async Task SetEnglishAsync()
    {
        // The culture picker is the only MudSelect on the dashboard; open it and pick English so the
        // rest of the flow can assert against the English strings.
        await _page.Locator(".mud-select").First.ClickAsync();
        await _page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = "English", Exact = true }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task RegisterAndVerifyAsync(string email, string password, string displayName)
    {
        await GotoAsync("/account/register");
        await _page.Locator("#register-name").FillAsync(displayName);
        await _page.Locator("#register-email").FillAsync(email);
        await _page.Locator("#register-password").FillAsync(password);
        await _page.Locator("#register-password").PressAsync("Enter");

        var (userId, token) = await ReadVerificationLinkAsync(email);
        await GotoAsync($"/verify-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");
    }

    private async Task PasswordSignInAsync(string email, string password)
    {
        await GotoAsync("/account/login");
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await _page.Locator("input[type=password]").First.FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        await _page.WaitForURLAsync($"{WebAuthnStackFixture.WebOrigin}/", new PageWaitForURLOptions { Timeout = 15_000 });
    }

    private async Task SignOutAsync()
    {
        await OpenAccountMenuAsync();
        await _page.GetByText("Sign out").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private Task OpenAccountMenuAsync() =>
        _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Account" }).First.ClickAsync();

    // ---- out-of-band helpers ---------------------------------------------------------------------

    // Reads the account-verification link from Mailpit (the dev mail sink), the same out-of-band step
    // a real user performs in their inbox.
    private static async Task<(string UserId, string Token)> ReadVerificationLinkAsync(string email)
    {
        for (var attempt = 0; attempt < 30; attempt++)
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

    // Reads the account's stored passkeys through the API (bearer token from a password login), and
    // returns the raw JSON too so the test can assert no key material is present in the surface.
    private async Task<(IReadOnlyList<PasskeyResponse> Passkeys, string RawJson)> ListStoredPasskeysAsync(
        string email, string password)
    {
        var session = await _stack.Api.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, password));
        session.EnsureSuccessStatusCode();
        var tokens = await session.Content.ReadFromJsonAsync<SessionResponse>()
            ?? throw new InvalidOperationException("The password login returned no session.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/passkeys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await _stack.Api.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        var passkeys = JsonSerializer.Deserialize<List<PasskeyResponse>>(raw, Json.Web) ?? [];

        return (passkeys, raw);
    }
}
