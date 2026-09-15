using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Starts one throwaway Postgres container for the shared-by-link store tests (FEAT-013, TP-013) and
/// applies the <c>shared</c> migration to it, so the store runs against the schema the application
/// deploys. Requires a container runtime; where none is available the tests fail rather than skip.
/// </summary>
public sealed class SharedGroupPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("shareddb_tests")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SharedGroupDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wired as the application wires it, optionally overriding the abuse limits.</summary>
    /// <param name="configureLimits">An optional override of the NFR-009 limits (e.g. a tiny cap for a cap test).</param>
    /// <returns>A service provider owning a <see cref="SharedGroupDbContext"/> and the store.</returns>
    public ServiceProvider CreateProvider(Action<SharedGroupLimits>? configureLimits = null)
    {
        var services = new ServiceCollection();
        services.AddSharedGroupsRepository(ConnectionString);
        services.Configure<SharedGroupLimits>(limits => configureLimits?.Invoke(limits));
        // A fixed 32-byte AES-256 key for the at-rest token cipher (ADR-0061); test-only, never a real key.
        services.Configure<SharedGroupSecurityOptions>(security =>
            security.TokenEncryptionKey = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()));

        return services.BuildServiceProvider();
    }
}

/// <summary>Shares one container across the shared-by-link store tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class SharedGroupDatabase : ICollectionFixture<SharedGroupPostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "shared-group-postgres";
}
