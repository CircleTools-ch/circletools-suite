// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.Main.Service.Repository;

/// <summary>Dependency-injection registration for the Platform sync store's Postgres persistence.</summary>
public static class SyncRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// The migrations-history table for the sync context, kept inside the <c>sync</c> schema so its
    /// migrations stay independent of Main's and Circles' — the same rule those two follow.
    /// </summary>
    internal const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>Registers the <see cref="SyncDbContext"/>, the sync store and its telemetry.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string (the same one Main uses).</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// Sync has its own context and its own schema inside Main's database, not its own database
    /// (ADR-0003). The telemetry sink is a singleton because it owns a meter (one per process).
    /// </remarks>
    public static IServiceCollection AddSyncRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(HistoryTableName, SyncDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISyncTelemetry, SyncCollisionTelemetry>();
        services.AddScoped<ISyncStore, SyncStore>();

        return services;
    }
}
