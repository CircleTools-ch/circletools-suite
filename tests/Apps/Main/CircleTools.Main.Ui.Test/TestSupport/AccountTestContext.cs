using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Localization;
using CircleTools.Main.Ui.Api;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Api;
using CircleTools.Ui.Account.Auth;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.Main.Ui.Test.TestSupport;

/// <summary>
/// A bUnit context wired the way <c>Main.WebApp</c> wires the real app, minus the browser.
/// </summary>
/// <remarks>
/// Composing the real <see cref="AccountApiClient"/>, <see cref="AccountSession"/> and
/// <see cref="TokenAuthStateProvider"/> over a stub transport is deliberate: the interesting
/// behaviour of these pages lives in how they react to what the API says, and a hand-written fake
/// client would test the fake.
/// </remarks>
public sealed class AccountTestContext : Bunit.TestContext
{
    /// <summary>The stub API both clients talk to.</summary>
    public StubApiHandler Api { get; } = new();

    /// <summary>Where a "download" ends up instead of the browser.</summary>
    public FakeFileSaver Files { get; } = new();

    /// <summary>Initialises the context and registers everything the account pages inject.</summary>
    /// <param name="passkeys">The passkey ceremony to use; a cancelling one by default.</param>
    public AccountTestContext(IPasskeyCeremony? passkeys = null)
    {
        var baseAddress = new Uri("https://api.test/");

        // MudBlazor (ADR-0040): the account pages are MudBlazor components now. They call JS for
        // ripples/element refs, so the JSInterop runs in loose mode (unmatched calls are no-ops)
        // rather than the strict default that would fail the render.
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Open generic: the pages use IStringLocalizer<AppStrings>, and the shared components
        // (BusyButton, CultureSelector) use IStringLocalizer<SharedStrings>.
        Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        Services.AddSingleton<IKeyValueStore, FakeKeyValueStore>();
        Services.AddSingleton<IFileSaver>(Files);
        Services.AddSingleton(passkeys ?? new StubPasskeyCeremony());

        Services.AddSingleton(new CultureOptions("test.culture"));
        Services.AddSingleton<ICultureProvider, CultureProvider>();

        Services.AddSingleton<IAccessTokenStore>(_ => new InMemoryAccessTokenStore(TimeProvider.System));
        Services.AddSingleton(new RefreshTokenStoreOptions("test.refreshToken"));
        Services.AddSingleton<IRefreshTokenStore, RefreshTokenStore>();
        Services.AddSingleton<TokenAuthStateProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthStateProvider>());
        Services.AddSingleton<SessionNotices>();
        Services.AddSingleton<ISessionRefresher, SessionRefresher>();
        Services.AddSingleton<AccountApiClient>();
        Services.AddSingleton<CirclesApiClient>();
        Services.AddSingleton<AccountSession>();

        // bUnit pre-registers placeholder authorization services that throw, to push tests towards
        // its AddTestAuthorization() fake. These tests want the real thing - the principal comes
        // from a real token through the real provider - so the placeholders are removed first;
        // AddAuthorizationCore only registers what is not already there.
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.AddAuthorizationCore();

        // Both keys point at the same stub; the split only matters for the refresh loop, which is
        // covered where it lives (CircleTools.ApiClient.Test).
        Services.AddKeyedSingleton(
            AccountApiClients.Anonymous,
            (_, _) => new HttpClient(Api) { BaseAddress = baseAddress });
        Services.AddKeyedSingleton(
            AccountApiClients.Authorized,
            (_, _) => new HttpClient(Api) { BaseAddress = baseAddress });
    }

    /// <summary>
    /// Renders a page the way the app does: inside <see cref="CascadingAuthenticationState"/>.
    /// </summary>
    /// <remarks>
    /// Anything containing an <c>AuthorizeView</c> throws without that cascade, and the account
    /// pages that matter all contain one.
    /// </remarks>
    /// <typeparam name="TComponent">The page to render.</typeparam>
    /// <returns>The rendered page.</returns>
    public IRenderedComponent<TComponent> RenderPage<TComponent>()
        where TComponent : IComponent =>
        this.RenderComponent<CascadingAuthenticationState>(parameters => parameters.AddChildContent<TComponent>())
            .FindComponent<TComponent>();

    /// <summary>
    /// Navigates so that a page's <c>[SupplyParameterFromQuery]</c> parameters are populated.
    /// </summary>
    /// <remarks>
    /// The mail links are query strings, so this is also the only way to exercise them the way a
    /// user arrives at them: bUnit refuses to set those parameters directly, and rightly so.
    /// </remarks>
    /// <param name="relativeUri">The path and query, without a leading slash.</param>
    public void NavigateTo(string relativeUri) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo(relativeUri);

    /// <summary>Puts the context in a signed-in state, as a token from the API would.</summary>
    /// <param name="displayName">The name shown in the UI.</param>
    /// <param name="email">The account's address.</param>
    /// <param name="emailVerified">Whether the address has been proven (FR-007).</param>
    /// <param name="userId">The account's id; a new one by default.</param>
    /// <returns>The signed-in user's id.</returns>
    public Guid SignIn(
        string displayName = "Anna",
        string email = "anna@example.test",
        bool emailVerified = true,
        Guid? userId = null)
    {
        var id = userId ?? Guid.CreateVersion7();

        Services.GetRequiredService<IAccessTokenStore>()
            .SetToken(Jwt(id, displayName, email, emailVerified), TimeSpan.FromMinutes(15));

        Services.GetRequiredService<TokenAuthStateProvider>().NotifyChanged();

        return id;
    }

    /// <summary>
    /// Builds an access token shaped like the one the API issues.
    /// </summary>
    /// <remarks>
    /// The signature is a placeholder. The client never verifies it — the API does, and inventing a
    /// real one here would only test the test.
    /// </remarks>
    /// <param name="userId">The subject.</param>
    /// <param name="displayName">The name claim.</param>
    /// <param name="email">The email claim.</param>
    /// <param name="emailVerified">The email_verified claim.</param>
    /// <param name="locale">The locale claim.</param>
    /// <returns>An encoded JWT.</returns>
    public static string Jwt(
        Guid userId,
        string displayName = "Anna",
        string email = "anna@example.test",
        bool emailVerified = true,
        string locale = "de-CH")
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = userId.ToString(),
            ["name"] = displayName,
            ["email"] = email,
            ["email_verified"] = emailVerified,
            ["locale"] = locale,
        });

        return $"{Segment("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{Segment(payload)}.signature";
    }

    private static string Segment(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
}
