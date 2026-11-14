// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Circles.Repository;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection string.
/// Never used at runtime — the host configures the context via DI.
/// </summary>
/// <remarks>
/// It goes through <see cref="CirclesRepositoryServiceCollectionExtensions.AddCirclesRepository"/>
/// rather than newing the context up, so the migration is scaffolded against exactly the
/// configuration the application runs — including the history table living in the <c>circles</c>
/// schema. A hand-built context here would generate migrations that record themselves in the wrong
/// place, which shows up as Main's migrations being replayed against a database that already has
/// them.
/// </remarks>
public sealed class CirclesDbContextFactory : IDesignTimeDbContextFactory<CirclesDbContext>
{
    /// <inheritdoc />
    public CirclesDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddCirclesRepository("Host=localhost;Database=maindb;Username=postgres;Password=postgres");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<CirclesDbContext>();
    }
}
