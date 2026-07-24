using Microsoft.Extensions.DependencyInjection;
using Walf.Application;
using Walf.Main.Contracts;
using Walf.Main.Service.Core.Application;

namespace Walf.Main.Service.Core;

/// <summary>Dependency-injection registration for the Main module's core (domain + application).</summary>
public static class MainCoreServiceCollectionExtensions
{
    /// <summary>Registers the Main module's command and query handlers.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMainCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingletonTimeProvider();

        services.AddScoped<ICommandHandler<CreateHeartbeatCommand, Guid>, CreateHeartbeatCommandHandler>();
        services.AddScoped<IQueryHandler<GetHeartbeatsQuery, IReadOnlyList<HeartbeatDto>>, GetHeartbeatsQueryHandler>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
