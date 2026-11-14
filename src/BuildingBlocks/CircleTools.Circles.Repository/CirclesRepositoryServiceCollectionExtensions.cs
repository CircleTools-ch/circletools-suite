// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Circles.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.Circles.Repository;

/// <summary>Dependency-injection registration for the Circles context's Postgres persistence.</summary>
public static class CirclesRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// The migrations-history table for this context, kept inside the <c>circles</c> schema.
    /// </summary>
    /// <remarks>
    /// Two contexts sharing one database must not share one history table, or each would try to
    /// apply the other's migrations. Its own table in its own schema is what makes the two migration
    /// sets independent — and what lets the Circles context be split into its own database later
    /// without rewriting either history.
    /// </remarks>
    internal const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>
    /// Registers the <see cref="CirclesDbContext"/> and the two ports this adapter supplies.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The other two ports are not here and cannot be: <c>IAccountDirectory</c> needs Identity and
    /// <c>IInvitationMailer</c> needs the outbox, and Circles references neither. The API host is the
    /// only place that has all four.
    /// <para>
    /// The connection string is the same one Main uses. Circles has its own context and its own
    /// schema inside that database, not its own database (ADR-0003).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddCirclesRepository(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<CirclesDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(HistoryTableName, CirclesDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEmailHasher, HmacEmailHasher>();
        services.AddScoped<ICircleRepository, EfCircleRepository>();
        services.AddScoped<IInvitationRateLimiter, EfInvitationRateLimiter>();

        return services;
    }

    /// <summary>Registers the circles retention purge (NFR-004).</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the <c>Circles:Retention</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCirclesRetention(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<CirclesRetentionOptions>(configuration.GetSection(CirclesRetentionOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CirclesPurge>();
        services.AddHostedService<CirclesRetentionService>();

        return services;
    }
}
