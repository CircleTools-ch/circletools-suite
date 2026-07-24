using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Walf.Main.Service.Core.Domain;

namespace Walf.Main.Service.Repository;

/// <summary>Dependency-injection registration for the Main module's Postgres persistence.</summary>
public static class MainRepositoryServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="MainDbContext"/> and repository adapters.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMainRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MainDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IHeartbeatRepository, HeartbeatRepository>();

        return services;
    }
}
