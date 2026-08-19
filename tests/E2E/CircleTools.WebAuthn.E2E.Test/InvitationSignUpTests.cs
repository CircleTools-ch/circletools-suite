using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CircleTools.Circles.Contracts;
using CircleTools.Main.Contracts;
using Microsoft.Playwright;

namespace CircleTools.WebAuthn.E2E.Test;

/// <summary>
/// Drives the sign-up-from-an-invitation-link journey end to end in Chromium (arc42 D8,
/// <see href="../../../docs/features/TP-003-m2-circles.md">TP-003</see> gap 7): an admin invites an
/// address that has <em>no account</em>, and the recipient lands on the mail link, creates an
/// account, verifies it, signs in and joins — one browser session, one continuous journey (FR-018).
/// </summary>
/// <remarks>
/// <para>
/// Both halves of this were already covered — M1's register → verify → sign in, and M2's accept — but
/// nothing drove a real mail link through sign-up in one test; the join was walked in a browser by
/// hand. This is the automation that closes that hole, and it reuses the D4 host
/// (<see cref="WebAuthnStackFixture"/>, ADR-0045) rather than standing up a second stack.
/// </para>
/// <para>
/// The <strong>inviter's</strong> side is setup, not the thing under test, so it runs over the API
/// (<see cref="WebAuthnStackFixture.Api"/>): register → verify → sign in → create a circle → invite the
/// recipient's address. The <strong>recipient's</strong> side — the journey D8 is about — is driven
/// through the browser exactly as a person would walk it, from the mailed link to membership.
/// </para>
/// <para>
/// The helpers here deliberately mirror <see cref="PasskeyCeremonyTests"/>' rather than being lifted
/// into a shared base: the ceremony test is closed and reviewed, and two browser E2E call sites do not
/// yet earn an abstraction over it. When a third arrives, extract then.
/// </para>
/// </remarks>
[Collection(WebAuthnE2E.CollectionName)]
public sealed class InvitationSignUpTests : IAsyncLifetime
{
    private static readonly HttpClient Mailpit = new() { BaseAddress = new Uri(WebAuthnStackFixture.MailpitApi) };

    private readonly WebAuthnStackFixture _stack;
    private readonly List<string> _consoleIssues = [];

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private IPage _page = default!;

    public InvitationSignUpTests(WebAuthnStackFixture stack) => _stack = stack;

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
            if (message.Type == "error" && IsRealFault(message.Text))
            {
                _consoleIssues.Add($"[error] {message.Text}");
            }
        };
        _page.PageError += (_, error) =>
        {
            if (IsRealFault(error))
            {
                _consoleIssues.Add($"[pageerror] {error}");
            }
        };
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Invitation_link_carries_a_new_person_through_sign_up_into_the_circle()
    {
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var inviterEmail = $"invite.inviter+{stamp}@circletools.test";
        var recipientEmail = $"invite.recipient+{stamp}@circletools.test";
        const string password = "Correct-Horse-9";
        const string inviterName = "Ada Inviter";
        const string recipientName = "Ben Recipient";
        const string circleName = "Invitation E2E Circle";

        // ---- Setup (API): the inviter creates a circle and invites an address with no account. ----
        var inviterToken = await ApiRegisterVerifyAndSignInAsync(inviterEmail, password, inviterName);
        var circleId = await ApiCreateCircleAsync(inviterToken, circleName);
        await ApiInviteAsync(inviterToken, circleId, recipientEmail);

        // ---- Journey (browser): the recipient walks the mail link through to membership. ----
        await GotoAsync("/");
        await SetEnglishAsync();

        // The mail to someone with no account links to the anonymous landing, not into the app. Follow
        // that exact link, as a person clicking their inbox would.
        var invitationLink = await ReadInvitationLinkAsync(recipientEmail);
        await _page.GotoAsync(invitationLink, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // The landing offers signing up *and* signing in and never says which applies (NFR-005). This
        // recipient has no account, so they take the sign-up door.
        await _page.GetByText("Create an account to join").ClickAsync();
        await RegisterViaFormAsync(recipientName, recipientEmail, password);

        // Verify the address the ordinary way — through the link in the verification mail.
        var (userId, token) = await ReadVerificationLinkAsync(recipientEmail);
        await GotoAsync($"/verify-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

        // Sign in. The register link carried no returnUrl by design: once the address is verified this
        // very invitation is waiting on the in-app path, which is where the journey resumes (FR-018).
        await PasswordSignInAsync(recipientEmail, password);

        await GotoAsync("/invitations");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Join" }).First.ClickAsync();

        // "You are in." — the accept succeeded and the recipient is now a member.
        await _page.GetByText("You are in.").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // ---- Ground truth (API): the server agrees the recipient joined. ----
        // Read the circle as the inviter. The people list now holds two account-backed members — the
        // owner and the person who just joined from the link — and the newcomer is a plain member, not
        // a managed profile. That the address never appears here is NFR-006; that they are in the
        // circle at all is the whole point of D8.
        var circle = await ApiGetCircleAsync(inviterToken, circleId);
        Assert.Equal(2, circle.People.Count);

        var joined = Assert.Single(circle.People, p => p.DisplayName == recipientName);
        Assert.False(joined.IsManaged);
        Assert.False(joined.IsYou); // "you" is the inviter reading the roster.
        Assert.Equal("member", joined.Role);

        Assert.True(_consoleIssues.Count == 0, "Console issues:\n" + string.Join("\n", _consoleIssues));
    }

    // Mirrors PasskeyCeremonyTests.IsRealFault: a console "error"/pageerror worth failing over — a
    // genuine application fault, not an artifact of the test host (subresource noise, or the service
    // worker refusing to register under an untrusted dev certificate on the CI host).
    private static bool IsRealFault(string text) =>
        !text.StartsWith("Failed to load resource", StringComparison.Ordinal)
        && !text.Contains("SSL certificate error", StringComparison.Ordinal)
        && !text.Contains("Failed to register a ServiceWorker", StringComparison.Ordinal);

    // ---- inviter-side setup, over the API ---------------------------------------------------------

    private async Task<string> ApiRegisterVerifyAndSignInAsync(string email, string password, string displayName)
    {
        var register = await _stack.Api.PostAsJsonAsync(
            "/api/v1/account/register", new RegisterRequest(displayName, email, password, "en-CH"));
        register.EnsureSuccessStatusCode();

        var (userId, token) = await ReadVerificationLinkAsync(email);

        var verify = await _stack.Api.PostAsJsonAsync(
            "/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));
        verify.EnsureSuccessStatusCode();

        var login = await _stack.Api.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<SessionResponse>(Json.Web)
            ?? throw new InvalidOperationException("The password login returned no session.");

        return session.AccessToken;
    }

    private async Task<Guid> ApiCreateCircleAsync(string bearer, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/circles")
        {
            Content = JsonContent.Create(new CreateCircleRequest(name, null)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        using var response = await _stack.Api.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var circle = await response.Content.ReadFromJsonAsync<CircleDetailResponse>(Json.Web)
            ?? throw new InvalidOperationException("Creating the circle returned no body.");

        return circle.Id;
    }

    private async Task ApiInviteAsync(string bearer, Guid circleId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/circles/{circleId}/invitations")
        {
            Content = JsonContent.Create(new InviteRequest(email, null)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        using var response = await _stack.Api.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<CircleDetailResponse> ApiGetCircleAsync(string bearer, Guid circleId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/circles/{circleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        using var response = await _stack.Api.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CircleDetailResponse>(Json.Web)
            ?? throw new InvalidOperationException("Reading the circle returned no body.");
    }

    // ---- browser helpers --------------------------------------------------------------------------

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

    private async Task RegisterViaFormAsync(string displayName, string email, string password)
    {
        // Clicking "Create an account to join" has already navigated to the register page.
        await _page.Locator("#register-name").FillAsync(displayName);
        await _page.Locator("#register-email").FillAsync(email);
        await _page.Locator("#register-password").FillAsync(password);
        await _page.Locator("#register-password").PressAsync("Enter");
    }

    private async Task PasswordSignInAsync(string email, string password)
    {
        await GotoAsync("/account/login");
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(email);
        await _page.Locator("input[type=password]").First.FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).First.ClickAsync();
        await _page.WaitForURLAsync($"{WebAuthnStackFixture.WebOrigin}/", new PageWaitForURLOptions { Timeout = 15_000 });
    }

    // ---- out-of-band helpers (Mailpit, the dev mail sink) -----------------------------------------

    private static readonly Regex InvitationLink = new(@"https?://[^\s""'<]+/invitation/[^\s""'<]+", RegexOptions.Compiled);

    // Reads the account-verification link (userId + token) from the verification mail addressed to the
    // given address — the same out-of-band step a real user performs in their inbox. Matched by content,
    // so it finds the right mail even when an invitation mail also sits in the same inbox.
    private static async Task<(string UserId, string Token)> ReadVerificationLinkAsync(string email)
    {
        var body = await ReadMailBodyAsync(email, b => b.Contains("userId=", StringComparison.Ordinal)
            && b.Contains("token=", StringComparison.Ordinal));

        var userId = Regex.Match(body, @"userId=([^&""'\s]+)");
        var token = Regex.Match(body, @"token=([^&""'\s]+)");

        return (Uri.UnescapeDataString(userId.Groups[1].Value), Uri.UnescapeDataString(token.Groups[1].Value));
    }

    // Reads the invitation link (the full landing URL, secret and all) from the invitation mail
    // addressed to the given address. The no-account recipient's mail points at /invitation/{secret},
    // never into the app — following it as-is is exactly what clicking the inbox link does.
    private static async Task<string> ReadInvitationLinkAsync(string email)
    {
        var body = await ReadMailBodyAsync(email, b => InvitationLink.IsMatch(b));
        return InvitationLink.Match(body).Value;
    }

    // Polls Mailpit for a message to <paramref name="email"/> whose body satisfies <paramref name="matches"/>,
    // returning that body. Matching by content rather than position lets a re-read after a new mail
    // arrives (e.g. the verification mail landing after the invitation) find the right one.
    private static async Task<string> ReadMailBodyAsync(string email, Func<string, bool> matches)
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

                if (matches(body ?? string.Empty))
                {
                    return body ?? string.Empty;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new InvalidOperationException($"No matching mail arrived for {email}.");
    }
}
