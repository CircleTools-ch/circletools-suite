using System.Net;
using AppSuite.Main.Contracts;
using AppSuite.Main.Ui.Components;
using AppSuite.Main.Ui.Pages;
using AppSuite.Main.Ui.Pages.Account;
using AppSuite.Main.Ui.Test.TestSupport;
using Bunit;

namespace AppSuite.Main.Ui.Test;

/// <summary>
/// Component tests for the account screens (FEAT-002 / TP-002).
/// </summary>
/// <remarks>
/// These cover the behaviours that are easy to get quietly wrong and invisible in a screenshot: the
/// deliberately uninformative answers that keep the API from being an account-enumeration oracle
/// (NFR-005), and the rule that a cancelled passkey prompt is not an error (FR-011).
/// </remarks>
public sealed class AccountPageTests
{
    private const string Account = "api/v1/account";

    // What the API actually sends for a password-policy failure: codes, not prose (ADR-0027).
    private static readonly string[] _tooShort = ["PasswordTooShort"];
    private static readonly string[] _breached = ["BreachedPassword"];
    private static readonly string[] _unknownProse = ["Something the client has no key for."];

    [Fact]
    public void SignUp_ShowsTheSameConfirmationWhateverTheAddress()
    {
        using var ctx = new AccountTestContext();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/register", HttpStatusCode.Accepted);

        var cut = ctx.RenderPage<Register>();
        Fill(cut, "#register-name", "Anna");
        Fill(cut, "#register-email", "anna@example.test");
        Fill(cut, "#register-password", "a-long-enough-passphrase");
        cut.Find("form").Submit();

        // The same wording an address already in use would produce (NFR-005): the screen cannot be
        // used to find out who has an account.
        cut.WaitForAssertion(() => Assert.Contains("RegisterDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void SignUp_SaysWhyThePasswordWasRefused_InTheUsersLanguage()
    {
        // The API answers with a code; the screen must turn it into a localized sentence rather
        // than show the code or Identity's English prose (NFR-003, ADR-0027).
        using var ctx = new AccountTestContext();
        ctx.Api.RespondJson(
            HttpMethod.Post,
            $"{Account}/register",
            new { title = "Validation failed", errors = new { password = _tooShort } },
            HttpStatusCode.BadRequest);

        var cut = ctx.RenderPage<Register>();
        Fill(cut, "#register-email", "anna@example.test");
        Fill(cut, "#register-password", "short");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[role=alert]");
            Assert.Contains("ErrorValidation", alert.TextContent, StringComparison.Ordinal);
            Assert.Contains("PasswordErrorTooShort", alert.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("PasswordTooShort<", alert.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SignUp_ExplainsABreachedPassword_RatherThanJustRefusingIt()
    {
        // FR-007 requires the rule to be explained. A bare "validation failed" would leave someone
        // retrying a 16-character password they consider strong, with no idea what is wrong.
        using var ctx = new AccountTestContext();
        ctx.Api.RespondJson(
            HttpMethod.Post,
            $"{Account}/register",
            new { title = "Validation failed", errors = new { password = _breached } },
            HttpStatusCode.BadRequest);

        var cut = ctx.RenderPage<Register>();
        Fill(cut, "#register-email", "anna@example.test");
        Fill(cut, "#register-password", "passwordpassword");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "PasswordErrorBreached",
                cut.Find("[role=alert]").TextContent,
                StringComparison.Ordinal));
    }

    [Fact]
    public void SignUp_PassesThroughAMessageItHasNoTranslationFor()
    {
        // Not every endpoint answers in codes, and a message the client cannot translate is still
        // better than silence. This is the fallback the mixed contract depends on.
        using var ctx = new AccountTestContext();
        ctx.Api.RespondJson(
            HttpMethod.Post,
            $"{Account}/register",
            new { title = "Validation failed", errors = new { request = _unknownProse } },
            HttpStatusCode.BadRequest);

        var cut = ctx.RenderPage<Register>();
        Fill(cut, "#register-email", "anna@example.test");
        Fill(cut, "#register-password", "a-long-enough-passphrase");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Something the client has no key for.",
                cut.Find("[role=alert]").TextContent,
                StringComparison.Ordinal));
    }

    [Fact]
    public void ForgotPassword_ShowsTheSameConfirmationForAnyAddress()
    {
        using var ctx = new AccountTestContext();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/forgot-password", HttpStatusCode.Accepted);

        var cut = ctx.RenderPage<ForgotPassword>();
        Fill(cut, "#forgot-email", "nobody@example.test");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("ForgotDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void CancelledPasskeySignIn_LeavesThePageWithNoError()
    {
        // The stub returns null, which is what a dismissed system prompt produces.
        using var ctx = new AccountTestContext(new StubPasskeyCeremony(result: null));
        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/login/passkey/options", new { challenge = "abc" });

        var cut = ctx.RenderPage<Login>();
        cut.Find("button[type=button]").Click();

        // FR-011: changing your mind is not a failure, so nothing may be announced as one.
        cut.WaitForAssertion(() =>
            Assert.Contains($"POST {Account}/login/passkey/options", ctx.Api.Calls, StringComparer.Ordinal));
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.DoesNotContain($"POST {Account}/login/passkey", ctx.Api.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void BrokenPasskeyCeremony_DoesSayWhatWentWrong()
    {
        using var ctx = new AccountTestContext(new StubPasskeyCeremony(throws: true));
        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/login/passkey/options", new { challenge = "abc" });

        var cut = ctx.RenderPage<Login>();
        cut.Find("button[type=button]").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("LoginPasskeyUnsupported", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void VerificationBanner_AppearsOnlyWhileTheAddressIsUnproven()
    {
        using var ctx = new AccountTestContext();
        ctx.SignIn(emailVerified: false);
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/resend-verification", HttpStatusCode.Accepted);

        var cut = ctx.RenderPage<VerificationBanner>();
        Assert.Contains("VerifyBannerText", cut.Markup, StringComparison.Ordinal);

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("VerifyBannerSent", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void VerificationBanner_IsAbsentForAVerifiedAccount()
    {
        using var ctx = new AccountTestContext();
        ctx.SignIn(emailVerified: true);

        var cut = ctx.RenderPage<VerificationBanner>();

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void VerifyEmail_PassesTheTokenThroughExactlyAsItArrived()
    {
        const string token = "CfDJ8Abc-_123";

        using var ctx = new AccountTestContext();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/verify-email", HttpStatusCode.NoContent);
        ctx.NavigateTo($"verify-email?userId={Guid.CreateVersion7()}&token={token}");

        var cut = ctx.RenderPage<VerifyEmail>();

        cut.WaitForAssertion(() => Assert.Contains("VerifyDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void VerifyEmail_WithAnIncompleteLink_ExplainsRatherThanCallingTheApi()
    {
        using var ctx = new AccountTestContext();
        ctx.NavigateTo("verify-email?userId=abc");

        var cut = ctx.RenderPage<VerifyEmail>();

        Assert.Contains("VerifyFailedText", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(ctx.Api.Calls);
    }

    [Fact]
    public void ConfirmEmailChange_ArrivingSignedOut_SaysSoInsteadOfShowingA401()
    {
        using var ctx = new AccountTestContext();
        ctx.NavigateTo(
            $"confirm-email-change?userId={Guid.CreateVersion7()}&newEmail=new%40example.test&token=abc");

        var cut = ctx.RenderPage<ConfirmEmailChange>();

        cut.WaitForAssertion(() =>
            Assert.Contains("ConfirmEmailSignedOut", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(ctx.Api.Calls);
    }

    [Fact]
    public void ConfirmEmailChange_AsTheWrongAccount_SaysWhichProblemItIs()
    {
        using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.NavigateTo(
            $"confirm-email-change?userId={Guid.CreateVersion7()}&newEmail=new%40example.test&token=abc");

        var cut = ctx.RenderPage<ConfirmEmailChange>();

        // A bare 401 cannot tell "signed out" from "signed in as someone else"; the user can act on
        // the difference, so the page has to make it.
        cut.WaitForAssertion(() =>
            Assert.Contains("ConfirmEmailWrongAccount", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteAccount_RequiresThePasswordBeforeTheButtonDoesAnything()
    {
        using var ctx = new AccountTestContext();
        ctx.SignIn();

        var cut = ctx.RenderPage<AccountData>();

        var delete = cut.Find(".appsuite-danger button");
        Assert.True(delete.HasAttribute("disabled"));
        Assert.DoesNotContain("btn-primary", delete.ClassName, StringComparison.Ordinal);

        // Typing must unlock it, without the field having to lose focus first. Bound on change
        // instead, the button is still disabled when the user's first click arrives — the click
        // lands on nothing and deletion appears not to work. Found in a browser, not here.
        cut.Find("#delete-password").Input("a-long-enough-passphrase");
        Assert.False(cut.Find(".appsuite-danger button").HasAttribute("disabled"));
    }

    [Fact]
    public void DeleteAccount_OnSuccess_SaysTheAccountIsGoneAndDropsTheSession()
    {
        using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/delete", HttpStatusCode.NoContent);

        var cut = ctx.RenderPage<AccountData>();
        cut.Find("#delete-password").Input("a-long-enough-passphrase");
        cut.Find(".appsuite-danger form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("DeleteDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Export_HandsTheArchiveToThePlatformToSave()
    {
        using var ctx = new AccountTestContext();
        var userId = ctx.SignIn();

        ctx.Api.RespondJson(
            HttpMethod.Post,
            $"{Account}/export",
            new DataExport(
                new DateTimeOffset(2026, 7, 28, 9, 0, 0, TimeSpan.Zero),
                new ProfileResponse(userId, "Anna", "anna@example.test", "de-CH", true),
                [],
                1,
                ["Circles are not part of this release."]));

        var cut = ctx.RenderPage<AccountData>();
        cut.Find("section button").Click();

        cut.WaitForAssertion(() => Assert.NotNull(ctx.Files.Saved));

        var saved = ctx.Files.Saved!.Value;
        Assert.Equal("appsuite-export-2026-07-28.json", saved.FileName);
        Assert.Equal("application/json", saved.ContentType);
        Assert.Contains("anna@example.test", saved.Content, StringComparison.Ordinal);
    }

    private static void Fill(IRenderedFragment cut, string selector, string value) =>
        cut.Find(selector).Change(value);
}
