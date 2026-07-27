using AppSuite.Identity;
using AppSuite.Identity.Tokens;
using AppSuite.Main.Service.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Repository;

/// <summary>Dependency-injection registration for the Main module's Postgres persistence.</summary>
public static class MainRepositoryServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="MainDbContext"/> and repository adapters.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMainRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MainDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IHeartbeatRepository, HeartbeatRepository>();
        services.AddScoped<IRefreshTokenChainStore, RefreshTokenChainStore>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity over <see cref="MainDbContext"/> with the suite's
    /// account rules (ADR-0009, FR-007).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The Identity builder, so callers can add token providers and passkeys.</returns>
    /// <remarks>
    /// <para>
    /// <strong>This call shapes the EF model, not just runtime behaviour.</strong>
    /// <c>Stores.SchemaVersion</c> decides whether passkey entities are mapped at all, so
    /// anything that builds the model — the host, the design-time migration factory, and the
    /// model tests — must go through this method. Constructing <see cref="MainDbContext"/>
    /// directly yields a different, passkey-less model.
    /// </para>
    /// <para>
    /// The password rules implement FR-007: length over composition, per NIST SP 800-63B. The
    /// breached-password check is a separate validator, not an <c>IdentityOptions</c> setting.
    /// </para>
    /// </remarks>
    public static IdentityBuilder AddAppSuiteIdentityCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddIdentityCore<AppSuiteUser>(options =>
            {
                // Gates passkey storage (FR-010). Greenfield database — nothing to stay
                // backwards-compatible with.
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

                // FR-007: minimum 12 characters, no composition rules, no forced rotation.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;

                // FR-007 (decided 2026-07-27): a pending account may sign in; what it may not do
                // is anything account-scoped. That restriction is enforced per-endpoint, not by
                // blocking sign-in here.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<MainDbContext>();
    }
}
