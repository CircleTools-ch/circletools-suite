using CircleTools.ApiClient;
using CircleTools.Application;
using CircleTools.SecretSanta.Core;
using CircleTools.SecretSanta.Core.Reveal;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.LocalStore.Linking;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.WebApp;
using CircleTools.SecretSanta.WebApp.Http;
using CircleTools.SecretSanta.WebApp.Interop;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account;
using CircleTools.Ui.Account.Api;
using CircleTools.Ui.Account.Auth;
using CircleTools.Ui.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The Main service's origin, where a signed-in account syncs and where the account API lives. It is
// configured in wwwroot/appsettings.json (ApiBaseUrl); with no session it is never contacted, so an
// account-free visitor is unaffected by its value (NFR-001).
var apiBaseUrl = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress);

// The browser implementations of the platform seams this app needs (ADR-0028). A MAUI head swaps
// exactly these registrations; everything above them is shared feature code.
builder.Services.AddScoped<IKeyValueStore, BrowserKeyValueStore>();
builder.Services.AddScoped<BrowserPasskeyCeremony>();
builder.Services.AddScoped<IPasskeyCeremony>(sp => sp.GetRequiredService<BrowserPasskeyCeremony>());

// Two clients over the same Main API, differing only in whether the authorizing handler is in the
// chain. See AccountApiClients for why they are separate; the short version is that refreshing a
// session must not run through the handler that triggers refreshes.
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

// Application + Secret-Santa module + the M3 client sync engine over the local store (FEAT-004). The
// engine works identically offline; SyncService only ever runs for a signed-in account, so the
// account-free product is unchanged (NFR-001).
builder.Services.AddApplication();
builder.Services.AddSecretSantaCore();
builder.Services.AddSecretSantaSyncEngine();

// The delta-exchange port over the account's authorized client (ADR-0021). Only claim / un-claim and
// a running sync cycle ever reach for it, and only once signed in.
builder.Services.AddScoped<ISyncApi>(sp =>
    new HttpSyncApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));

// The M4 linking surface over the same authorized client (FEAT-005): member-links, the assignment
// projection and self-owned wishlists. Inert for account-free users and unlinked members (NFR-001).
builder.Services.AddScoped<ILinkingApi>(sp =>
    new HttpLinkingApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));

// The read-only circle directory for the create-from-circle picker (FEAT-005): an authorized read of
// the caller's circles on the Main API (NFR-006). No circle data is written locally (ADR-0033).
builder.Services.AddScoped<ICircleDirectoryApi>(sp =>
    new HttpCircleDirectoryApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));

// The shared account/session UI (ADR-0034), under this app's own refresh-token key so a device that
// hosts more than one app never crosses their sessions (ADR-0026).
builder.Services.AddAccountUi("secretsanta.refreshToken");

// Reveal links point at this app's own origin unless RevealBaseUrl overrides it.
var configuredRevealBaseUrl = builder.Configuration["RevealBaseUrl"];
var revealBaseUrl = string.IsNullOrWhiteSpace(configuredRevealBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredRevealBaseUrl;
builder.Services.AddSingleton(new RevealLinkOptions(revealBaseUrl));

// Localization (ADR-0011) via the shared block, under Secret-Santa's own storage key (ADR-0025).
builder.Services.AddSecretSantaUi();

var host = builder.Build();

// Both of these must happen before the first render, and both need services that only exist once the
// host is built. The culture, because components read the CultureInfo defaults as they first render
// and a late switch would leave the first paint in the wrong language. The session, because the
// access token lives in memory only (ADR-0021) — every reload starts signed out, and it is the
// refresh token that makes the session look continuous.
await host.Services.GetRequiredService<ICultureProvider>().InitializeAsync();
await host.Services.GetRequiredService<AccountSession>().RestoreAsync();

await host.RunAsync();
