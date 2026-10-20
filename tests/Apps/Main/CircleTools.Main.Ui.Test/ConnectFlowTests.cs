using System.Net;
using CircleTools.Main.Contracts;
using CircleTools.Main.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using CircleTools.Ui.Account.Components;
using CircleTools.Ui.Account.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Ui.Test;

/// <summary>
/// Component tests for the client side of the cross-app single-sign-on hand-off (FEAT-006 / TP-006,
/// FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// These cover the two things that fail invisibly on screen and are missed by the server-side
/// integration tests: a requesting app must offer the affordance only to a signed-out visitor, and a
/// broken exchange must land on the app's own sign-in rather than a blank dead-end (NFR-001). The code
/// path they exercise — <see cref="SsoConnect"/> over the real <c>AccountApiClient</c>/
/// <c>AccountSession</c> — is the same one the browser runs; only the transport is stubbed.
/// </remarks>
public sealed class ConnectFlowTests
{
    private const string ConnectToken = "api/v1/account/connect/token";

    // The refresh-token storage key AccountTestContext configures (RefreshTokenStoreOptions).
    private const string RefreshTokenKey = "test.refreshToken";

    // A distinct origin from bUnit's http://localhost/, so the app under test is a *requesting* app.
    private const string Authority = "https://circletools.test";

    [Fact]
    public async Task ConnectCallback_WhenTheExchangeIsRefused_FallsBackToItsOwnSignIn_NotADeadEnd()
    {
        await using var ctx = new AccountTestContext();
        var sso = RegisterSso(ctx);

        // The authority sent us back with a code, but the API refuses to redeem it (expired, replayed,
        // or a PKCE mismatch). The client must turn that into the app's own sign-in, never a dead-end.
        ctx.Api.RespondProblem(HttpMethod.Post, ConnectToken, HttpStatusCode.BadRequest, "InvalidGrant");
        await sso.BeginAsync(); // stashes a real PKCE verifier, as the requesting app does before it redirects

        ctx.NavigateTo("account/connect/callback?code=abc123");
        var cut = ctx.RenderPage<ConnectCallback>();

        cut.WaitForAssertion(() =>
        {
            // It actually attempted the exchange, then fell back — not a silent no-op.
            Assert.Contains($"POST {ConnectToken}", ctx.Api.Calls, StringComparer.Ordinal);
            Assert.Contains("ConnectFailedText", cut.Markup, StringComparison.Ordinal);
            // The fallback is a real route into the app's own sign-in.
            Assert.Contains("account/login", cut.Markup, StringComparison.Ordinal);
        });

        // The user was not dropped onto the app root as though signed in.
        Assert.Contains("connect/callback", NavigationOf(ctx).Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectCallback_OnASuccessfulExchange_LandsSignedIn_AndLeavesTheCallbackUrl()
    {
        await using var ctx = new AccountTestContext();
        var sso = RegisterSso(ctx);

        // The token exchange returns the same SessionResponse a password sign-in does (parity, FR-008).
        ctx.Api.RespondJson(
            HttpMethod.Post,
            ConnectToken,
            new SessionResponse(AccountTestContext.Jwt(Guid.CreateVersion7()), 900, "refresh-token", EmailVerified: true));
        await sso.BeginAsync();

        ctx.NavigateTo("account/connect/callback?code=abc123");
        var cut = ctx.RenderPage<ConnectCallback>();

        var nav = NavigationOf(ctx);

        // Adopting the session navigates to the app root, which is what strips the ?code from the URL.
        cut.WaitForAssertion(() => Assert.Equal(nav.BaseUri, nav.Uri));
        Assert.DoesNotContain("code=", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectCallback_OnASuccessfulExchange_PersistsTheSessionAcrossTabs()
    {
        await using var ctx = new AccountTestContext();
        var sso = RegisterSso(ctx);

        ctx.Api.RespondJson(
            HttpMethod.Post,
            ConnectToken,
            new SessionResponse(AccountTestContext.Jwt(Guid.CreateVersion7()), 900, "refresh-token", EmailVerified: true));
        await sso.BeginAsync();

        ctx.NavigateTo("account/connect/callback?code=abc123");
        var cut = ctx.RenderPage<ConnectCallback>();

        cut.WaitForAssertion(() => Assert.Equal(NavigationOf(ctx).BaseUri, NavigationOf(ctx).Uri));

        // A hand-off spares a second sign-in, so its session must survive a new tab: the refresh token
        // lands in durable (localStorage) scope, which a fresh tab reads, not per-tab session scope.
        var store = ctx.Services.GetRequiredService<IKeyValueStore>();
        Assert.Equal("refresh-token", await store.GetAsync(StorageScope.Durable, RefreshTokenKey));
        Assert.Null(await store.GetAsync(StorageScope.Session, RefreshTokenKey));
    }

    [Fact]
    public async Task ContinueWithAccount_OffersItself_ToASignedOutVisitorOnARequestingApp()
    {
        await using var ctx = new AccountTestContext();
        RegisterSso(ctx);

        var cut = ctx.RenderPage<ContinueWithAccount>();

        Assert.Contains("ConnectContinue", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContinueWithAccount_IsAbsent_OnceThereIsALocalSession()
    {
        await using var ctx = new AccountTestContext();
        RegisterSso(ctx);
        ctx.SignIn();

        var cut = ctx.RenderPage<ContinueWithAccount>();

        // A user who already has a session on this app has nothing to bootstrap.
        Assert.DoesNotContain("ConnectContinue", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContinueWithAccount_SuppressesItself_OnTheAuthorityOrigin()
    {
        await using var ctx = new AccountTestContext();

        // The authority origin equals this app's own origin: it never hands off to itself.
        RegisterSso(ctx, authority: "http://localhost/");

        var cut = ctx.RenderPage<ContinueWithAccount>();

        Assert.Empty(cut.Markup.Trim());
    }

    /// <summary>
    /// Registers the SSO flow the way an app that consumes the account block does. Deliberately local
    /// to these tests: the shared <see cref="AccountTestContext"/> leaves <see cref="SsoConnect"/>
    /// unregistered so the affordance renders nothing there, which is what the other account-page tests
    /// rely on.
    /// </summary>
    private static SsoConnect RegisterSso(AccountTestContext ctx, string authority = Authority)
    {
        ctx.Services.AddSingleton(new SsoConnectOptions(authority, "requesting-app"));
        ctx.Services.AddSingleton<SsoConnect>();
        return ctx.Services.GetRequiredService<SsoConnect>();
    }

    private static NavigationManager NavigationOf(AccountTestContext ctx) =>
        ctx.Services.GetRequiredService<NavigationManager>();
}
