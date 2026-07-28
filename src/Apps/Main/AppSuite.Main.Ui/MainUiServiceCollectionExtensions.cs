using AppSuite.ApiClient;
using AppSuite.Main.Ui.Api;
using AppSuite.Main.Ui.Auth;
using Microsoft.AspNetCore.Components.Authorization;
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
    /// Adds the account API client, the token stores and the authentication state provider.
    /// </summary>
    /// <remarks>
    /// The host must still register an <see cref="AppSuite.Ui.Abstractions.IKeyValueStore"/>, an
    /// <see cref="AppSuite.Ui.Abstractions.IPasskeyCeremony"/>, and the two keyed
    /// <see cref="HttpClient"/>s named in <see cref="MainApiClients"/> — those are the parts that
    /// differ between a browser and a device, which is the whole reason they are seams.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMainAccount(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorizationCore();

        services.AddScoped<IAccessTokenStore>(_ => new InMemoryAccessTokenStore(TimeProvider.System));
        services.AddSingleton(new RefreshTokenStoreOptions(RefreshTokenStorageKey));
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        // Registered twice on purpose: AuthorizeView resolves the base type, while the session
        // classes need the concrete one to announce a change. Both must be the same instance.
        services.AddScoped<TokenAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthStateProvider>());

        services.AddScoped<ISessionRefresher, SessionRefresher>();
        services.AddScoped<AccountApiClient>();
        services.AddScoped<AccountSession>();

        return services;
    }
}
