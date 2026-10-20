using CircleTools.Circles.Repository;
using CircleTools.Identity;
using CircleTools.Main.Service.Core.Domain;
using CircleTools.Main.Service.Repository;
using CircleTools.Main.Service.Repository.Linking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Starts one throwaway Postgres container for the business-metrics count test (FEAT-022, TP-022) and
/// applies the four migrated contexts the collector counts (Main/Identity, Circles, Shared, Linking) so
/// the count queries run against the real deployed schema. Requires a container runtime; where none is
/// available the tests fail rather than skip (the same contract as the other repository fixtures).
/// </summary>
public sealed class BusinessMetricsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("businessmetrics_tests")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // One database, four schemas, four independent migration histories (ADR-0003).
        await services.GetRequiredService<MainDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CirclesDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<SharedGroupDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<LinkingDbContext>().Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wiring the four counted contexts as the host wires them.</summary>
    /// <returns>A service provider owning the four contexts the collector reads.</returns>
    public ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddMainRepository(ConnectionString);
        services.AddCircleToolsIdentityCore();
        services.AddCirclesRepository(ConnectionString);
        services.AddSharedGroupsRepository(ConnectionString);
        services.Configure<SharedGroupLimits>(_ => { });
        // A fixed 32-byte AES-256 key for the at-rest token cipher (ADR-0061); test-only, never a real key.
        // Unused by the count queries, but AddSharedGroupsRepository expects the options to be present.
        services.Configure<SharedGroupSecurityOptions>(security =>
            security.TokenEncryptionKey = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()));
        services.AddLinkingRepository(ConnectionString);

        return services.BuildServiceProvider();
    }
}

/// <summary>Shares one container across the business-metrics count test.</summary>
[CollectionDefinition(CollectionName)]
public sealed class BusinessMetricsDatabase : ICollectionFixture<BusinessMetricsPostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "business-metrics-postgres";
}
