// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Service.Core.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CircleTools.SecretSanta.Service.Repository;

/// <summary>Registers the Secret-Santa group store (ADR-0086).</summary>
public static class SecretSantaRepositoryServiceCollectionExtensions
{
    private const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>
    /// Adds the store over the main database, in its own schema with its own migration history. The host supplies
    /// the platform ports (<see cref="IGroupSecrets"/>, <see cref="IGroupAccounts"/>, <see cref="IGroupNotifications"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The main database's connection string.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSecretSantaGroupStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<SecretSantaDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(HistoryTableName, SecretSantaDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IGroupStore, GroupStore>();
        services.AddScoped<GroupService>();

        // The host binds GroupLimits to configuration; unbound, the defaults apply.
        services.AddOptions<GroupLimits>();
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<GroupLimits>>().Value);
        return services;
    }
}

/// <summary>Builds the context at design time, for <c>dotnet ef migrations</c>.</summary>
public sealed class SecretSantaDbContextFactory : IDesignTimeDbContextFactory<SecretSantaDbContext>
{
    /// <inheritdoc />
    public SecretSantaDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSecretSantaGroupStore("Host=localhost;Database=maindb;Username=postgres;Password=postgres");
        return services.BuildServiceProvider().GetRequiredService<SecretSantaDbContext>();
    }
}
