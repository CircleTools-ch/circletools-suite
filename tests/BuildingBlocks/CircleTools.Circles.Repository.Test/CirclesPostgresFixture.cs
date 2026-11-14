// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CircleTools.Circles.Repository.Test;

/// <summary>
/// Starts one throwaway Postgres container for the whole collection and applies the circles
/// migrations to it, so the tests run against the schema the application actually deploys rather
/// than against <c>EnsureCreated</c>.
/// </summary>
/// <remarks>
/// <para>
/// Running the migration is itself an assertion. <c>xmin</c> is a Postgres system column and cannot
/// be created; the generated migration lists it among the circle's columns and relies on the
/// provider filtering system columns out of the emitted SQL. If that ever stopped holding, every
/// test in this project would fail at startup — which is the right place to find out.
/// </para>
/// <para>
/// Requires a container runtime. Where none is available the tests fail rather than silently skip:
/// an integration suite that quietly does nothing is worse than one that is red.
/// </para>
/// </remarks>
public sealed class CirclesPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("circlesdb_tests")
        .Build();

    /// <summary>Gets the connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirclesDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds a provider wired exactly as the application wires it.</summary>
    /// <returns>A service provider owning a <see cref="CirclesDbContext"/>.</returns>
    /// <remarks>
    /// Each call yields an independent provider, so a test can hold two of them and act as two
    /// concurrent requests — which is the only way to exercise the concurrency token honestly.
    /// </remarks>
    public ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddCirclesRepository(ConnectionString);

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Shares one container across the integration tests. Named without the conventional xUnit
/// "Collection" suffix because CA1711 reserves that for actual collection types.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class SharedCirclesDatabase : ICollectionFixture<CirclesPostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "circles-postgres";
}
