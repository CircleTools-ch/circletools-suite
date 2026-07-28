using AppSuite.Application;
using AppSuite.SecretSanta.Core;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.LocalStore;
using AppSuite.SecretSanta.Ui;
using AppSuite.SecretSanta.WebApp;
using AppSuite.Ui.Abstractions;
using AppSuite.Ui.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The browser implementation of the one platform seam this app needs (ADR-0028). A MAUI head swaps
// exactly this registration; everything above it is shared feature code.
builder.Services.AddScoped<IKeyValueStore, BrowserKeyValueStore>();

// Local-first: no backend. Application + Secret-Santa module + the repository over that store.
builder.Services.AddApplication();
builder.Services.AddSecretSantaCore();
builder.Services.AddSecretSantaGroupRepository();

// Reveal links point at this app's own origin unless RevealBaseUrl overrides it.
var configuredRevealBaseUrl = builder.Configuration["RevealBaseUrl"];
var revealBaseUrl = string.IsNullOrWhiteSpace(configuredRevealBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredRevealBaseUrl;
builder.Services.AddSingleton(new RevealLinkOptions(revealBaseUrl));

// Localization (ADR-0011) via the shared block, under Secret-Santa's own storage key (ADR-0025).
builder.Services.AddSecretSantaUi();

var host = builder.Build();

// The culture must be resolved before the host runs, because components read the CultureInfo
// defaults as they first render — a late switch would leave the first paint in the wrong language.
// Reading it needs the key/value store, which only exists once the host is built; hence build,
// initialize, then run.
await host.Services.GetRequiredService<ICultureProvider>().InitializeAsync();

await host.RunAsync();
