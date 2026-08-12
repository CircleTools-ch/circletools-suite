using AppSuite.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppSuite.Main.Service.Repository.Linking;

/// <summary>Dependency-injection registration for the M4 linking store's Postgres persistence.</summary>
public static class LinkingRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// The migrations-history table for the linking context, kept inside the <c>linking</c> schema so
    /// its migrations stay independent of Main's, sync's and Circles' — the same rule those follow.
    /// </summary>
    internal const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>Registers the <see cref="LinkingDbContext"/> and the <see cref="ILinkingStore"/>.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string (the same one Main uses).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddLinkingRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<LinkingDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(HistoryTableName, LinkingDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILinkingStore, LinkingStore>();

        return services;
    }
}
