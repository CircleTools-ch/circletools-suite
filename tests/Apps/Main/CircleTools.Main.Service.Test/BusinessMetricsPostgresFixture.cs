// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Circles.Repository;
using CircleTools.Identity;
using CircleTools.Main.Service.Repository;
using CircleTools.SecretSanta.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Starts one throwaway Postgres container for the business-metrics count test (FEAT-022, TP-022) and
/// applies the three migrated contexts the collector counts (Main/Identity, Circles, Secret Santa) so
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

        // One database, three schemas, three independent migration histories (ADR-0003).
        await services.GetRequiredService<MainDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CirclesDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<SecretSantaDbContext>().Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wiring the three counted contexts as the host wires them.</summary>
    /// <returns>A service provider owning the three contexts the collector reads.</returns>
    public ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddMainRepository(ConnectionString);
        services.AddCircleToolsIdentityCore();
        services.AddCirclesRepository(ConnectionString);
        services.AddSecretSantaGroupStore(ConnectionString);

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
