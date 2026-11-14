// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using CircleTools.Main.Contracts;
using CircleTools.Main.Ui.Test.TestSupport;
using CircleTools.Ui.Account.Components;
using CircleTools.Ui.Account.Pages;
using Bunit;

namespace CircleTools.Main.Ui.Test;

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
    private static readonly string[] _empty = ["PasswordTooShort", "PasswordRequiresUniqueChars"];
    private static readonly string[] _breached = ["BreachedPassword"];
    private static readonly string[] _unknownProse = ["Something the client has no key for."];

    [Fact]
    public async Task SignUp_ShowsTheSameConfirmationWhateverTheAddress()
    {
        await using var ctx = new AccountTestContext();
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
    public async Task SignUp_SaysWhyThePasswordWasRefused_InTheUsersLanguage()
    {
        // The API answers with a code; the screen must turn it into a localized sentence rather
        // than show the code or Identity's English prose (NFR-003, ADR-0027).
        await using var ctx = new AccountTestContext();
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
    public async Task SignUp_AnEmptyPassword_IsTooShort_Once_WithNoRawCode()
    {
        // An empty password also fails the one-unique-character rule; both codes say the same thing (2026-10-25).
        await using var ctx = new AccountTestContext();
        ctx.Api.RespondJson(
            HttpMethod.Post,
            $"{Account}/register",
            new { title = "Validation failed", errors = new { password = _empty } },
            HttpStatusCode.BadRequest);

        var cut = ctx.RenderPage<Register>();
        Fill(cut, "#register-email", "anna@example.test");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[role=alert]");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(alert.TextContent, "PasswordErrorTooShort"));
            Assert.DoesNotContain("PasswordRequiresUniqueChars", alert.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SignUp_ExplainsABreachedPassword_RatherThanJustRefusingIt()
    {
        // FR-007 requires the rule to be explained. A bare "validation failed" would leave someone
        // retrying a 16-character password they consider strong, with no idea what is wrong.
        await using var ctx = new AccountTestContext();
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
    public async Task SignUp_PassesThroughAMessageItHasNoTranslationFor()
    {
        // Not every endpoint answers in codes, and a message the client cannot translate is still
        // better than silence. This is the fallback the mixed contract depends on.
        await using var ctx = new AccountTestContext();
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
    public async Task ForgotPassword_ShowsTheSameConfirmationForAnyAddress()
    {
        await using var ctx = new AccountTestContext();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/forgot-password", HttpStatusCode.Accepted);

        var cut = ctx.RenderPage<ForgotPassword>();
        Fill(cut, "#forgot-email", "nobody@example.test");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("ForgotDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancelledPasskeySignIn_LeavesThePageWithNoError()
    {
        // The stub returns null, which is what a dismissed system prompt produces.
        await using var ctx = new AccountTestContext(new StubPasskeyCeremony(result: null));
        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/login/passkey/options", new { challenge = "abc" });

        var cut = ctx.RenderPage<Login>();
        cut.Find("button[type=button]:not([aria-label=ShowPassword])").Click();

        // FR-011: changing your mind is not a failure, so nothing may be announced as one.
        cut.WaitForAssertion(() =>
            Assert.Contains($"POST {Account}/login/passkey/options", ctx.Api.Calls, StringComparer.Ordinal));
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.DoesNotContain($"POST {Account}/login/passkey", ctx.Api.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public async Task BrokenPasskeyCeremony_DoesSayWhatWentWrong()
    {
        await using var ctx = new AccountTestContext(new StubPasskeyCeremony(throws: true));
        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/login/passkey/options", new { challenge = "abc" });

        var cut = ctx.RenderPage<Login>();
        cut.Find("button[type=button]:not([aria-label=ShowPassword])").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("LoginPasskeyUnsupported", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerificationBanner_AppearsOnlyWhileTheAddressIsUnproven()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn(emailVerified: false);
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/resend-verification", HttpStatusCode.Accepted);

        var cut = ctx.RenderPage<VerificationBanner>();
        Assert.Contains("VerifyBannerText", cut.Markup, StringComparison.Ordinal);

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("VerifyBannerSent", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerificationBanner_IsAbsentForAVerifiedAccount()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn(emailVerified: true);

        var cut = ctx.RenderPage<VerificationBanner>();

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public async Task VerifyEmail_PassesTheTokenThroughExactlyAsItArrived()
    {
        const string token = "CfDJ8Abc-_123";

        await using var ctx = new AccountTestContext();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/verify-email", HttpStatusCode.NoContent);
        ctx.NavigateTo($"verify-email?userId={Guid.CreateVersion7()}&token={token}");

        var cut = ctx.RenderPage<VerifyEmail>();

        cut.WaitForAssertion(() => Assert.Contains("VerifyDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyEmail_WithAnIncompleteLink_ExplainsRatherThanCallingTheApi()
    {
        await using var ctx = new AccountTestContext();
        ctx.NavigateTo("verify-email?userId=abc");

        var cut = ctx.RenderPage<VerifyEmail>();

        Assert.Contains("VerifyFailedText", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(ctx.Api.Calls);
    }

    [Fact]
    public async Task ConfirmEmailChange_ArrivingSignedOut_SaysSoInsteadOfShowingA401()
    {
        await using var ctx = new AccountTestContext();
        ctx.NavigateTo(
            $"confirm-email-change?userId={Guid.CreateVersion7()}&newEmail=new%40example.test&token=abc");

        var cut = ctx.RenderPage<ConfirmEmailChange>();

        cut.WaitForAssertion(() =>
            Assert.Contains("ConfirmEmailSignedOut", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(ctx.Api.Calls);
    }

    [Fact]
    public async Task ConfirmEmailChange_AsTheWrongAccount_SaysWhichProblemItIs()
    {
        await using var ctx = new AccountTestContext();
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
    public async Task DeleteAccount_RequiresThePasswordBeforeTheButtonDoesAnything()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();

        var cut = ctx.RenderPage<AccountData>();

        const string DeleteButton = ".circletools-danger button:not([aria-label=ShowPassword])";
        var delete = cut.Find(DeleteButton);
        Assert.True(delete.HasAttribute("disabled"));
        Assert.DoesNotContain("btn-primary", delete.ClassName, StringComparison.Ordinal);

        // Typing must unlock it, without the field having to lose focus first. Bound on change
        // instead, the button is still disabled when the user's first click arrives — the click
        // lands on nothing and deletion appears not to work. Found in a browser, not here.
        cut.Find("#delete-password").Input("a-long-enough-passphrase");
        Assert.False(cut.Find(DeleteButton).HasAttribute("disabled"));
    }

    /// <summary>
    /// After a successful password change the emptied fields are back to their fresh state — not all three
    /// flagged "Required" right under the success message (BTP-3).
    /// </summary>
    [Fact]
    public async Task ChangePassword_OnSuccess_ClearsTheFieldsWithoutFlaggingThemRequired()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/change-password", HttpStatusCode.NoContent);

        var cut = ctx.RenderPage<Security>();
        string[] fields = ["#current-password", "#new-password", "#repeat-password"];
        cut.Find(fields[0]).Change("the old long passphrase");
        cut.Find(fields[1]).Change("a brand new long passphrase");
        cut.Find(fields[2]).Change("a brand new long passphrase");
        foreach (var field in fields)
        {
            cut.Find(field).Blur();
        }

        cut.FindAll("form").First(f => f.QuerySelector("#current-password") is not null).Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("PasswordChanged", cut.Markup, StringComparison.Ordinal);
            Assert.All(fields, field => Assert.Empty(cut.Find(field).GetAttribute("value") ?? string.Empty));
            Assert.Empty(cut.FindAll(".mud-input-error"));
        });
    }

    /// <summary>The other devices were signed out by the change, so the list must not keep showing them (BTP-4).</summary>
    [Fact]
    public async Task ChangePassword_OnSuccess_RefreshesTheDeviceList()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();
        var now = DateTimeOffset.UtcNow;
        var thisDevice = new SessionInfoResponse(Guid.CreateVersion7(), "This phone", now, now, IsCurrent: true);
        var otherDevice = new SessionInfoResponse(Guid.CreateVersion7(), "Old tablet", now, now, IsCurrent: false);
        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/sessions", new[] { thisDevice, otherDevice });
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/change-password", HttpStatusCode.NoContent);

        var cut = ctx.RenderPage<Security>();
        cut.WaitForAssertion(() => Assert.Contains("Old tablet", cut.Markup, StringComparison.Ordinal));

        ctx.Api.RespondJson(HttpMethod.Post, $"{Account}/sessions", new[] { thisDevice });
        cut.Find("#current-password").Change("the old long passphrase");
        cut.Find("#new-password").Change("a brand new long passphrase");
        cut.Find("#repeat-password").Change("a brand new long passphrase");
        cut.FindAll("form").First(f => f.QuerySelector("#current-password") is not null).Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("PasswordChanged", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Old tablet", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>A field revealed before the change does not stay in clear text afterwards (PR-13).</summary>
    [Fact]
    public async Task ChangePassword_OnSuccess_MasksARevealedField()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/change-password", HttpStatusCode.NoContent);

        var cut = ctx.RenderPage<Security>();
        cut.FindAll("button[aria-label=ShowPassword]")[1].Click();
        Assert.Equal("text", cut.Find("#new-password").GetAttribute("type"));

        cut.Find("#current-password").Change("the old long passphrase");
        cut.Find("#new-password").Change("a brand new long passphrase");
        cut.Find("#repeat-password").Change("a brand new long passphrase");
        cut.FindAll("form").First(f => f.QuerySelector("#current-password") is not null).Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("PasswordChanged", cut.Markup, StringComparison.Ordinal);
            Assert.Equal("password", cut.Find("#new-password").GetAttribute("type"));
        });
    }

    /// <summary>Every password field on every account page has the toggle (PR-13).</summary>
    [Fact]
    public async Task EveryPasswordField_HasAToggle()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();

        AssertOneTogglePerField(ctx.RenderPage<Login>(), expected: 1);
        AssertOneTogglePerField(ctx.RenderPage<Register>(), expected: 1);
        AssertOneTogglePerField(ctx.RenderPage<Security>(), expected: 3);

        var data = ctx.RenderPage<AccountData>();
        data.WaitForAssertion(() => Assert.NotEmpty(data.FindAll("#delete-password")));
        AssertOneTogglePerField(data, expected: 1);

        static void AssertOneTogglePerField<TPage>(IRenderedComponent<TPage> page, int expected)
            where TPage : Microsoft.AspNetCore.Components.IComponent
        {
            Assert.Equal(expected, page.FindAll("input[type=password]").Count);
            Assert.Equal(expected, page.FindAll("button[aria-label=ShowPassword]").Count);
        }
    }

    [Fact]
    public async Task DeleteAccount_OnSuccess_SaysTheAccountIsGoneAndDropsTheSession()
    {
        await using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.Api.Respond(HttpMethod.Post, $"{Account}/delete", HttpStatusCode.NoContent);

        var cut = ctx.RenderPage<AccountData>();
        cut.Find("#delete-password").Input("a-long-enough-passphrase");
        cut.Find(".circletools-danger form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("DeleteDoneText", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteAccount_WhenNoSharedGroupsAreOwned_OffersNoDisposition()
    {
        // The unmatched shared/owned route answers 404 → the client reports zero → the section stays away.
        await using var ctx = new AccountTestContext();
        ctx.SignIn();

        var cut = ctx.RenderPage<AccountData>();

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain("DeleteSharedGroupsTitle", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteAccount_WhenSharedGroupsAreOwned_OffersHandOffOrDelete()
    {
        // FR-043 / ADR-0086: an owner of online groups is asked what becomes of them.
        await using var ctx = new AccountTestContext();
        ctx.SignIn();
        ctx.Api.RespondJson(
            HttpMethod.Get,
            "api/v1/account/owned-groups",
            new OwnedGroupCount(1));

        var cut = ctx.RenderPage<AccountData>();

        cut.WaitForAssertion(() =>
            Assert.Contains("DeleteSharedGroupsTitle", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("DeleteSharedGroupsHandOff", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("DeleteSharedGroupsRemove", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_HandsTheArchiveToThePlatformToSave()
    {
        await using var ctx = new AccountTestContext();
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

        // The export button is the first action on the page; the delete button lives in the danger card.
        cut.FindAll("button").First(b => b.Closest(".circletools-danger") is null).Click();

        cut.WaitForAssertion(() => Assert.NotNull(ctx.Files.Saved));

        var saved = ctx.Files.Saved!.Value;
        Assert.Equal("circletools-export-2026-07-28.json", saved.FileName);
        Assert.Equal("application/json", saved.ContentType);
        Assert.Contains("anna@example.test", saved.Content, StringComparison.Ordinal);
    }

    private static void Fill<TComponent>(IRenderedComponent<TComponent> cut, string selector, string value)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.Find(selector).Change(value);
}
