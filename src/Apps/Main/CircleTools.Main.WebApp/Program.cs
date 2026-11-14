// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.ApiClient;
using CircleTools.Localization;
using CircleTools.Main.Ui;
using CircleTools.Ui.Account;
using CircleTools.Ui.Account.Api;
using CircleTools.Ui.Account.Auth;
using CircleTools.Main.WebApp;
using CircleTools.Main.WebApp.Http;
using CircleTools.Main.WebApp.Interop;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API base address is configured in wwwroot/appsettings.json (ApiBaseUrl).
var apiBaseUrl = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress);

// Browser implementations of the platform seams. Everything above these is portable; a MAUI head
// would swap exactly these three registrations and reuse the rest unchanged.
builder.Services.AddScoped<IKeyValueStore, BrowserKeyValueStore>();
builder.Services.AddScoped<IFileSaver, BrowserFileSaver>();
builder.Services.AddScoped<BrowserPasskeyCeremony>();
builder.Services.AddScoped<IPasskeyCeremony>(sp => sp.GetRequiredService<BrowserPasskeyCeremony>());

// Two clients over the same API, differing only in whether the authorizing handler is in the chain.
// See AccountApiClients for why they are separate; the short version is that refreshing a session
// must not run through the handler that triggers refreshes.
builder.Services.AddKeyedScoped<HttpClient>(AccountApiClients.Anonymous, (_, _) =>
    new HttpClient(new BrowserCredentialsHandler { InnerHandler = new HttpClientHandler() })
    {
        BaseAddress = apiBaseUrl,
    });

builder.Services.AddKeyedScoped<HttpClient>(AccountApiClients.Authorized, (sp, _) =>
{
    var handler = new AuthorizedHttpMessageHandler(
        sp.GetRequiredService<IAccessTokenStore>(),
        sp.GetRequiredService<ISessionRefresher>())
    {
        InnerHandler = new BrowserCredentialsHandler { InnerHandler = new HttpClientHandler() },
    };

    return new HttpClient(handler) { BaseAddress = apiBaseUrl };
});

// An unkeyed HttpClient bound to the authorized (session-carrying) client, so anything that reaches
// for a plain HttpClient gets a session rather than silent 401s.
builder.Services.AddScoped(sp => sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized));

// Every AuthorizeView an anonymous visitor renders logs "Authorization failed:
// DenyAnonymousAuthorizationRequirement" at Information level. Being signed out is the normal state
// of this app, not an event, and three of those on the dashboard drown the console messages that do
// mean something.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);

// MudBlazor component services (ADR-0040): theme provider, popovers, dialogs, snackbars.
builder.Services.AddMudServices();

// The shared theme state behind the System/Light/Dark switch, persisted via IKeyValueStore.
builder.Services.AddScoped<CircleTools.Ui.Shared.ThemeState>();

// Localization (ADR-0011) and the account/session services (FEAT-002).
builder.Services.AddCircleToolsLocalization(MainUiServiceCollectionExtensions.CultureStorageKey);
builder.Services.AddMainAccount();

// Cross-app SSO (FR-031, ADR-0039). This app is the authority: it serves the /account/connect page
// that mints codes for sibling apps. Registering with its own origin as the authority means the
// interactive "continue" affordance suppresses itself here — the authority never hands off to itself.
var ssoAuthorityUrl = builder.Configuration["SsoAuthorityUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddAccountSso(ssoAuthorityUrl, "main-web");

// The cross-app switcher catalogue (web only, ADR-0013/0040): the suite's apps, from
// wwwroot/appsettings.json. Each is a SuiteAppLink the shared shell and dashboard render; this host's
// own entry is flagged IsCurrent by matching its key. Separate origins, so switching is a full
// navigation, not routing. MAUI heads register none of these, so the switcher self-hides there.
foreach (var entry in builder.Configuration.GetSection("SuiteApps").GetChildren())
{
    builder.Services.AddSingleton(new CircleTools.Ui.Shared.SuiteAppLink(
        Name: entry["Name"] ?? string.Empty,
        Url: entry["Url"] ?? string.Empty,
        LogoUrl: entry["LogoUrl"] ?? string.Empty,
        IsCurrent: string.Equals(entry["Key"], "main-web", StringComparison.Ordinal)));
}

var host = builder.Build();

// Both of these must happen before the first render, and both need services that only exist once
// the host is built. The culture, because components read CultureInfo as they render and a late
// switch would leave the first paint in the wrong language (UC-MAIN-005). The session, because the
// access token lives in memory only (ADR-0021) - every reload starts with nothing, and it is the
// refresh token that makes the session look continuous.
await host.Services.GetRequiredService<ICultureProvider>().InitializeAsync();
await host.Services.GetRequiredService<AccountSession>().RestoreAsync();

await host.RunAsync();
