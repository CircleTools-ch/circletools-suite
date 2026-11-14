// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;
using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>Dependency-injection registration for the local-first persistence adapters.</summary>
public static class SecretSantaLocalStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the account sync of the per-account documents (FEAT-004, ADR-0041): the <see cref="ILocalSyncStore"/>
    /// holding the profile, the preferences and the device's cursor, and the <see cref="SyncService"/> that runs a
    /// cycle. Groups do not travel here (ADR-0086).
    /// </summary>
    /// <remarks>
    /// The host supplies <see cref="ISyncApi"/> — normally <see cref="HttpSyncApi"/> over the account's authorized
    /// <see cref="HttpClient"/> (ADR-0021). The store itself is the host's too: a web host registers
    /// <c>CircleTools.Ui.Web.BrowserKeyValueStore</c>, the MAUI head <see cref="FileKeyValueStore"/> via
    /// <see cref="AddSecretSantaFileStorage"/> (ADR-0025).
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaSyncEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILocalSyncStore, LocalSyncStore>();
        services.AddScoped<SyncService>();
        return services;
    }

    /// <summary>
    /// Registers this device's groups (ADR-0086, FEAT-027): the group records, the domain repository over them, the
    /// one sync for every online group, and the portable backup of the groups held here (FEAT-017). The host
    /// registers <see cref="Online.IOnlineGroupApi"/> over its authorized client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSecretSantaOnlineGroups(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<Online.IGroupRecordStore, Online.GroupRecordStore>();
        services.AddScoped<Online.OnlineGroupSync>();
        services.AddScoped<IGroupRepository, Online.GroupRecordRepository>();
        services.AddScoped<BackupService>();
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
