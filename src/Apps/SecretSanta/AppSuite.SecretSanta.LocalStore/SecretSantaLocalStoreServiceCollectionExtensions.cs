using AppSuite.SecretSanta.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>Dependency-injection registration for the local-first persistence adapters.</summary>
public static class SecretSantaLocalStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers browser <c>localStorage</c> persistence: a <see cref="LocalStorageKeyValueStore"/>
    /// and the <see cref="GroupRepository"/> over it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaLocalStoreBrowser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IKeyValueStore, LocalStorageKeyValueStore>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        return services;
    }

    /// <summary>
    /// Registers file-based persistence under <paramref name="directory"/>: a
    /// <see cref="FileKeyValueStore"/> and the <see cref="GroupRepository"/> over it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="directory">The directory to store data files in (e.g. the app data directory).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaLocalStoreFile(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        services.AddSingleton(new FileKeyValueStoreOptions(directory));
        services.AddSingleton<IKeyValueStore, FileKeyValueStore>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        return services;
    }
}
