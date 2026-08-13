using CircleTools.SecretSanta.Core.Domain;
using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.LocalStore;

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
    /// registers <c>CircleTools.Ui.Web.BrowserKeyValueStore</c>, the MAUI head
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
    /// Registers the M3 client sync engine (FEAT-004): the syncing <see cref="IGroupRepository"/> over
    /// the shared <see cref="ILocalSyncStore"/>, and the <see cref="SyncService"/> that runs a cycle.
    /// </summary>
    /// <remarks>
    /// This replaces <see cref="AddSecretSantaGroupRepository"/> — the syncing repository serves the
    /// same port and works identically offline, additionally keeping the sync metadata a signed-in
    /// account needs. It migrates the existing local groups in place on first load, so it is safe to
    /// adopt for account-free users who never sign in: <see cref="SyncService"/> only ever runs for a
    /// signed-in account, so the account-free product is unchanged by construction.
    /// <para>
    /// The host supplies <see cref="ISyncApi"/> — normally <see cref="HttpSyncApi"/> over the account's
    /// authorized <see cref="HttpClient"/> (ADR-0021) — when it wires sign-in; the engine itself takes
    /// no dependency on how the token is obtained.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaSyncEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILocalSyncStore, LocalSyncStore>();
        services.AddScoped<IGroupRepository, SyncingGroupRepository>();
        services.AddScoped<SyncService>();
        services.AddScoped<ClaimableGroupsService>();
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
