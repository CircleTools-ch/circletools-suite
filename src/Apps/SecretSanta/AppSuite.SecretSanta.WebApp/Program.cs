using System.Globalization;
using AppSuite.Application;
using AppSuite.SecretSanta.Core;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.LocalStore;
using AppSuite.SecretSanta.Ui;
using AppSuite.SecretSanta.WebApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Local-first: no backend. Application + Secret-Santa module + browser localStorage persistence.
builder.Services.AddApplication();
builder.Services.AddSecretSantaCore();
builder.Services.AddSecretSantaLocalStoreBrowser();

// Reveal links point at this app's own origin unless RevealBaseUrl overrides it.
var configuredRevealBaseUrl = builder.Configuration["RevealBaseUrl"];
var revealBaseUrl = string.IsNullOrWhiteSpace(configuredRevealBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredRevealBaseUrl;
builder.Services.AddSingleton(new RevealLinkOptions(revealBaseUrl));

// Localization (ADR-0011): RESX + IStringLocalizer, default culture de-CH.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var host = builder.Build();

// The culture must be set before the host runs, because components read the CultureInfo defaults
// as they first render. Reading localStorage needs IJSRuntime, which only exists once the host is
// built - hence build, resolve, then run.
var js = host.Services.GetRequiredService<IJSRuntime>();
string? storedCulture = null;
try
{
    storedCulture = await js.InvokeAsync<string?>("localStorage.getItem", AppCultures.StorageKey);
}
catch (JSException)
{
    // Storage can be unavailable (private mode, blocked cookies); fall back to the default culture.
}

var culture = AppCultures.Resolve(storedCulture);
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
