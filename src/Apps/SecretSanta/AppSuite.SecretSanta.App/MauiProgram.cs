using System.Globalization;
using AppSuite.Application;
using AppSuite.Localization;
using AppSuite.SecretSanta.Core;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.LocalStore;
using AppSuite.SecretSanta.Ui;
using Microsoft.Extensions.Logging;

namespace AppSuite.SecretSanta.App;

/// <summary>Composition root for the MAUI Blazor Hybrid host.</summary>
public static class MauiProgram
{
    /// <summary>
    /// The base URL the reveal links point at. In development this is the local Blazor WebApp;
    /// replace with the deployed reveal origin for release builds.
    /// </summary>
    public const string RevealBaseUrl = "https://localhost:7081";

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

        // Local-first: no backend. Application + Secret-Santa module + file-based persistence.
        // The file store is this host's implementation of the IKeyValueStore seam (ADR-0025); the
        // web head registers the browser one instead and shares everything above it.
        builder.Services.AddApplication();
        builder.Services.AddSecretSantaCore();
        builder.Services.AddSecretSantaFileStorage(FileSystem.AppDataDirectory);
        builder.Services.AddSecretSantaGroupRepository();
        builder.Services.AddSingleton(new RevealLinkOptions(RevealBaseUrl));

        // Localization (ADR-0011) via the shared block, under Secret-Santa's own storage key.
        builder.Services.AddSecretSantaUi();

        // This head has no language picker yet, so nothing writes the stored preference and the
        // default is all that applies. ICultureProvider.InitializeAsync — which is what reads a
        // stored choice back — needs a service scope and an await that CreateMauiApp cannot give
        // it; wiring that up belongs with adding the picker, not with this migration.
        var defaultCulture = AppCultures.Resolve(null);
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
