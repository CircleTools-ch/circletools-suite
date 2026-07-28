using AppSuite.SecretSanta.Core.Domain;
using AppSuite.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>Dependency-injection registration for the local-first persistence adapters.</summary>
public static class SecretSantaLocalStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="GroupRepository"/> over whatever <see cref="IKeyValueStore"/> the
    /// host has supplied.
    /// </summary>
    /// <remarks>
    /// The store itself is deliberately not registered here. It is the one part that differs
    /// between a browser and a device, which is why ADR-0025 puts it in the host: a web host
    /// registers <c>AppSuite.Ui.Web.BrowserKeyValueStore</c>, the MAUI head
    /// <see cref="FileKeyValueStore"/> via <see cref="AddSecretSantaFileStorage"/>.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaGroupRepository(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGroupRepository, GroupRepository>();
        return services;
    }

    /// <summary>
    /// Registers file-based storage under <paramref name="directory"/> as the host's
    /// <see cref="IKeyValueStore"/>, for hosts with a file system instead of web storage.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="directory">The directory to store data files in (e.g. the app data directory).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaFileStorage(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        services.AddSingleton(new FileKeyValueStoreOptions(directory));
        services.AddSingleton<IKeyValueStore, FileKeyValueStore>();
        return services;
    }
}
