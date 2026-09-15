using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.Main.Service.Repository;

/// <summary>Dependency-injection registration for the shared-by-link group store (ADR-0058, FEAT-013).</summary>
public static class SharedGroupRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// The migrations-history table for the shared context, kept inside the <c>shared</c> schema so
    /// its migrations stay independent of the other four contexts'.
    /// </summary>
    internal const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>Registers the <see cref="SharedGroupDbContext"/>, the store and the token factory.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string (the same one Main uses).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSharedGroupsRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<SharedGroupDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(HistoryTableName, SharedGroupDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IShareTokenFactory, RandomShareTokenFactory>();
        services.TryAddSingleton<IShareTokenCipher, AesGcmShareTokenCipher>();
        services.AddScoped<ISharedGroupStore, SharedGroupStore>();

        return services;
    }
}
