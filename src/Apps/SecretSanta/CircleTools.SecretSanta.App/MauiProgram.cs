using CircleTools.ApiClient;
using CircleTools.Application;
using CircleTools.SecretSanta.App.Platform;
using CircleTools.SecretSanta.Core;
using CircleTools.SecretSanta.Core.Reveal;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.LocalStore.Linking;
using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account;
using CircleTools.Ui.Account.Api;
using Microsoft.Extensions.Logging;

namespace CircleTools.SecretSanta.App;

/// <summary>Composition root for the MAUI Blazor Hybrid host.</summary>
/// <remarks>
/// This head is at web parity (FEAT-002..005): the same shared feature code as the browser WebApp,
/// with the platform seams swapped for their device implementations — <see cref="FileKeyValueStore"/>
/// for storage, <see cref="MauiPasskeyCeremony"/> for the (deferred) passkey ceremony, and plain
/// <see cref="HttpClientHandler"/>s for the two account clients. Everything above those seams is the
/// identical code the WebApp registers.
/// </remarks>
public static class MauiProgram
{
    /// <summary>
    /// The base URL the reveal links point at. In development this is the local Blazor WebApp;
    /// replace with the deployed reveal origin for release builds.
    /// </summary>
    public const string RevealBaseUrl = "https://localhost:7081";

    /// <summary>
    /// The Main service origin — where a signed-in account syncs and where the account API lives.
    /// </summary>
    /// <remarks>
    /// Development default. With no session it is never contacted, so an account-free user is
    /// unaffected by its value (NFR-001). On the Android emulator the host loopback is reachable as
    /// <c>10.0.2.2</c> (not <c>localhost</c>); a physical device needs the machine's LAN address. The
    /// literal production origin lands with the suite domain (still deferred — see naming.md).
    /// </remarks>
    public const string ApiBaseUrl = "https://10.0.2.2:7080";

    /// <summary>Builds and configures the MAUI application.</summary>
    /// <returns>The configured <see cref="MauiApp"/>.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var apiBaseUrl = new Uri(ApiBaseUrl);

        // The device implementations of the platform seams (ADR-0028): storage, the passkey ceremony,
        // and (just below) the two account HTTP clients. The browser web host swaps exactly these;
        // everything else registered here is the identical shared feature code.
        builder.Services.AddSecretSantaFileStorage(FileSystem.AppDataDirectory);
        builder.Services.AddScoped<IPasskeyCeremony, MauiPasskeyCeremony>();

        // Two clients over the same Main API, differing only in whether the authorizing handler is in
        // the chain (see AccountApiClients). The device head needs no browser-credentials handler, so
        // the inner handler is a plain HttpClientHandler.
        builder.Services.AddKeyedScoped<HttpClient>(AccountApiClients.Anonymous, (_, _) =>
            new HttpClient(new HttpClientHandler()) { BaseAddress = apiBaseUrl });

        builder.Services.AddKeyedScoped<HttpClient>(AccountApiClients.Authorized, (sp, _) =>
        {
            var handler = new AuthorizedHttpMessageHandler(
                sp.GetRequiredService<IAccessTokenStore>(),
                sp.GetRequiredService<ISessionRefresher>())
            {
                InnerHandler = new HttpClientHandler(),
            };

            return new HttpClient(handler) { BaseAddress = apiBaseUrl };
        });

        // Application + Secret-Santa module + the M3 client sync engine over the local store (FEAT-004).
        // The engine works identically offline; SyncService only ever runs for a signed-in account, so
        // the account-free product is unchanged (NFR-001).
        builder.Services.AddApplication();
        builder.Services.AddSecretSantaCore();
        builder.Services.AddSecretSantaSyncEngine();

        // The delta-exchange, linking and circle-directory ports over the account's authorized client
        // (ADR-0021). All three are inert until signed in.
        builder.Services.AddScoped<ISyncApi>(sp =>
            new HttpSyncApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));
        builder.Services.AddScoped<ILinkingApi>(sp =>
            new HttpLinkingApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));
        builder.Services.AddScoped<ICircleDirectoryApi>(sp =>
            new HttpCircleDirectoryApi(sp.GetRequiredKeyedService<HttpClient>(AccountApiClients.Authorized)));

        // The shared account/session UI (ADR-0034), under this app's own refresh-token key so a device
        // that hosts more than one app never crosses their sessions (ADR-0026).
        builder.Services.AddAccountUi("secretsanta.refreshToken");

        // Reveal links point at the configured reveal origin.
        builder.Services.AddSingleton(new RevealLinkOptions(RevealBaseUrl));

        // Localization (ADR-0011) via the shared block, under Secret-Santa's own storage key (ADR-0025).
        // The stored culture is applied at startup by AppInitializer, before the first page renders.
        builder.Services.AddSecretSantaUi();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
