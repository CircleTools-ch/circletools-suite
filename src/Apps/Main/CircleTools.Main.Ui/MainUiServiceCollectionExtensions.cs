using CircleTools.Main.Ui.Api;
using CircleTools.Ui.Account;
using CircleTools.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Ui;

/// <summary>Registers the Main app's feature services in a host's composition root.</summary>
public static class MainUiServiceCollectionExtensions
{
    /// <summary>The key the refresh token is remembered under, per app (ADR-0026).</summary>
    public const string RefreshTokenStorageKey = "main.refreshToken";

    /// <summary>The key the chosen language is remembered under, per app (ADR-0011).</summary>
    public const string CultureStorageKey = "main.culture";

    /// <summary>
    /// Adds the shared account/session services (ADR-0034) plus the Main-only circles client.
    /// </summary>
    /// <remarks>
    /// The account/session plumbing and pages live in <c>CircleTools.Ui.Account</c> now; this delegates
    /// to <see cref="AccountUiServiceCollectionExtensions.AddAccountUi"/> under the Main app's own
    /// refresh-token key and adds <see cref="CirclesApiClient"/>, which is Main-only (ADR-0033). The
    /// host must still register an <see cref="CircleTools.Ui.Abstractions.IKeyValueStore"/>, an
    /// <see cref="CircleTools.Ui.Abstractions.IPasskeyCeremony"/>, and the two keyed
    /// <see cref="HttpClient"/>s named in <c>AccountApiClients</c> — the parts that differ between a
    /// browser and a device, which is the whole reason they are seams.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMainAccount(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAccountUi(RefreshTokenStorageKey);
        services.AddScoped<CirclesApiClient>();

        // Per-app header identity (ADR-0040 amendment): the suite ring mark and the brand navy on the
        // app bar. Head-agnostic, so it lives here rather than in a host — the logo is served same-origin
        // from the shared building block, which works on both the WASM and MAUI heads.
        services.AddSingleton(new AppBranding(
            LogoUrl: "_content/CircleTools.Ui.Shared/img/app-main.svg",
            AppBarColor: "#03173d",
            AppBarText: "#e7e9ee"));

        return services;
    }
}
