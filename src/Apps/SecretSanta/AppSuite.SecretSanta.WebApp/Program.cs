using System.Globalization;
using AppSuite.Application;
using AppSuite.SecretSanta.Core;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.LocalStore;
using AppSuite.SecretSanta.WebApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

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

var defaultCulture = new CultureInfo("de-CH");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

await builder.Build().RunAsync();
