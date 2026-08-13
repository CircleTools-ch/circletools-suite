using CircleTools.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Starts one throwaway Postgres container for the whole integration collection and applies the
/// migrations to it, so tests run against the schema the application actually deploys rather
/// than against <c>EnsureCreated</c>.
/// </summary>
/// <remarks>
/// Requires a container runtime. Where none is available the tests fail rather than silently
/// skip — an integration suite that quietly does nothing is worse than one that is red.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("maindb_tests")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wired exactly as the application wires it.</summary>
    /// <returns>A service provider owning a <see cref="MainDbContext"/>.</returns>
    public ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddMainRepository(ConnectionString);
        services.AddCircleToolsIdentityCore();

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Shares one container across the integration tests. Named without the conventional xUnit
/// "Collection" suffix because CA1711 reserves that for actual collection types.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class SharedPostgresDatabase : ICollectionFixture<PostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "postgres";
}
