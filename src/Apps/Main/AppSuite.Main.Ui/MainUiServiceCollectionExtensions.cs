using AppSuite.Main.Ui.Api;
using AppSuite.Ui.Account;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Ui;

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
    /// The account/session plumbing and pages live in <c>AppSuite.Ui.Account</c> now; this delegates
    /// to <see cref="AccountUiServiceCollectionExtensions.AddAccountUi"/> under the Main app's own
    /// refresh-token key and adds <see cref="CirclesApiClient"/>, which is Main-only (ADR-0033). The
    /// host must still register an <see cref="AppSuite.Ui.Abstractions.IKeyValueStore"/>, an
    /// <see cref="AppSuite.Ui.Abstractions.IPasskeyCeremony"/>, and the two keyed
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

        return services;
    }
}
