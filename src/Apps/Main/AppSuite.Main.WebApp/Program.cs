using AppSuite.ApiClient;
using AppSuite.Localization;
using AppSuite.Main.Ui;
using AppSuite.Ui.Account.Api;
using AppSuite.Ui.Account.Auth;
using AppSuite.Main.WebApp;
using AppSuite.Main.WebApp.Http;
using AppSuite.Main.WebApp.Interop;
using AppSuite.Ui.Abstractions;
using AppSuite.Ui.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

// The unkeyed HttpClient the Heartbeat sample injects. It is the authorized one, so anything added
// later that reaches for a plain HttpClient gets a session rather than silent 401s.
builder.Services.AddScoped(sp => sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized));

// Every AuthorizeView an anonymous visitor renders logs "Authorization failed:
// DenyAnonymousAuthorizationRequirement" at Information level. Being signed out is the normal state
// of this app, not an event, and three of those on the dashboard drown the console messages that do
// mean something.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);

// Localization (ADR-0011) and the account/session services (FEAT-002).
builder.Services.AddAppSuiteLocalization(MainUiServiceCollectionExtensions.CultureStorageKey);
builder.Services.AddMainAccount();

var host = builder.Build();

// Both of these must happen before the first render, and both need services that only exist once
// the host is built. The culture, because components read CultureInfo as they render and a late
// switch would leave the first paint in the wrong language (UC-MAIN-005). The session, because the
// access token lives in memory only (ADR-0021) - every reload starts with nothing, and it is the
// refresh token that makes the session look continuous.
await host.Services.GetRequiredService<ICultureProvider>().InitializeAsync();
await host.Services.GetRequiredService<AccountSession>().RestoreAsync();

await host.RunAsync();
