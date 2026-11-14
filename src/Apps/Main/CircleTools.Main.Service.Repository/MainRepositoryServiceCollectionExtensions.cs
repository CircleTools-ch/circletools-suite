// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Identity;
using CircleTools.Identity.Passwords;
using CircleTools.Identity.Tokens;
using CircleTools.Infrastructure.Mail;
using CircleTools.Infrastructure.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.Main.Service.Repository;

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
        services.AddScoped<IRefreshTokenChainStore, RefreshTokenChainStore>();
        services.AddScoped<ISessionUserLookup, SessionUserLookup>();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<IOutboxDispatchStore, OutboxDispatchStore>();

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
    /// breached-password check is a separate validator, not an <c>IdentityOptions</c> setting —
    /// see <see cref="BreachedPasswordValidator"/> (ADR-0027).
    /// </para>
    /// </remarks>
    public static IdentityBuilder AddCircleToolsIdentityCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singleton: the corpus is parsed once per process, keeping the sign-up path free of I/O
        // (NFR-005 timing).
        services.TryAddSingleton<IBreachedPasswordList, EmbeddedBreachedPasswordList>();

        // Scoped, because IPasswordHasher<T> is (NFR-005 — see DecoyPasswordVerifier).
        services.TryAddScoped<DecoyPasswordVerifier>();

        return services.AddIdentityCore<CircleToolsUser>(options =>
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
            .AddPasswordValidator<BreachedPasswordValidator>()
            .AddEntityFrameworkStores<MainDbContext>();

        // NOTE: token providers are NOT registered here. AddIdentityCore omits them, and
        // AddDefaultTokenProviders lives in the ASP.NET Core framework assembly, which this
        // library deliberately does not reference. The API host adds them — without that,
        // GenerateEmailConfirmationTokenAsync (FR-007) and reset tokens (FR-009) throw at runtime.
    }

    /// <summary>
    /// Registers outbound mail and the background dispatcher (ADR-0023, NFR-005).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the <c>Mail</c> and <c>Outbox</c> sections.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCircleToolsMail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.Configure<OutboxDispatcherOptions>(configuration.GetSection(OutboxDispatcherOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IMailTemplateRenderer, MailTemplateRenderer>();
        services.AddSingleton<IMailSender, SmtpMailSender>();
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxDispatcherService>();

        return services;
    }

    /// <summary>
    /// Registers the retention purges for unverified accounts and sent outbox mail (NFR-004).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the <c>Retention</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCircleToolsRetention(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RetentionOptions>(configuration.GetSection(RetentionOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PendingAccountPurge>();
        services.AddScoped<OutboxPurge>();
        services.AddHostedService<MainRetentionService>();

        return services;
    }

    /// <summary>
    /// Registers session issuance and refresh (ADR-0021), binding lifetimes and signing
    /// parameters from configuration.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the <c>Auth</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCircleToolsSessions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<TokenLifetimeOptions>(configuration.GetSection(TokenLifetimeOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AccessTokenIssuer>();
        services.AddScoped<SessionService>();

        return services;
    }
}
