using CircleTools.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Starts one throwaway Postgres container for the sync tests and applies the <c>sync</c> migration to
/// it, so the store runs against the schema the application actually deploys rather than against
/// <c>EnsureCreated</c> (TP-004). Applying the migration as part of setup is itself an assertion.
/// </summary>
/// <remarks>
/// Requires a container runtime. Where none is available the tests fail rather than silently skip —
/// an integration suite that quietly does nothing is worse than one that is red.
/// </remarks>
public sealed class SyncPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("syncdb_tests")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wired exactly as the application wires it.</summary>
    /// <returns>A service provider owning a <see cref="SyncDbContext"/>.</returns>
    /// <remarks>
    /// Each call yields an independent provider, so a test can hold several and drive them as
    /// concurrent cycles — the only honest way to exercise the per-account sequence lock.
    /// </remarks>
    public ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSyncRepository(ConnectionString);

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Shares one container across the sync tests. Named without the conventional xUnit "Collection"
/// suffix because CA1711 reserves that for actual collection types.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class SharedSyncDatabase : ICollectionFixture<SyncPostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "sync-postgres";
}
