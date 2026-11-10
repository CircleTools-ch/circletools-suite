using CircleTools.Circles.Repository;
using CircleTools.Main.Service.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Boots the real API against a throwaway Postgres, so the account endpoints are exercised through
/// the whole stack — routing, model binding, Identity, EF and JWT validation.
/// </summary>
public sealed class AccountApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("maindb_api_tests")
        .Build();

    /// <summary>The application clock, which reports what a constant-time route's handler cost.</summary>
    public ConstantTimeClock ConstantTimeClock { get; } = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _database.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MainDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SyncDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CirclesDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development would re-run migrations on startup against the wrong connection string.
        builder.UseEnvironment(Environments.Production);

        // UseSetting rather than ConfigureAppConfiguration: under the minimal hosting model the
        // app reads configuration while Program's top-level statements run, which is too early for
        // callbacks added here. Host settings are in place before that.
        builder.UseSetting("ConnectionStrings:maindb", _database.GetConnectionString());

        // These tests share one source address and would otherwise trip the NFR-005 rate limit.
        // The limiter has its own dedicated coverage in RateLimitingTests.
        builder.UseSetting("RateLimiting:SensitivePermitLimit", "100000");
        builder.UseSetting("Auth:Jwt:Issuer", "circletools");
        builder.UseSetting("Auth:Jwt:Audience", "circletools-api");
        builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-long-enough-for-hmac");
        // Test-only at-rest keys (ADR-0061/0062); real values are host-provided secrets (ADR-0050).
        builder.UseSetting("SharedGroups:Security:TokenEncryptionKey", "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        builder.UseSetting("Circles:Security:EmailHashKey", "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        builder.UseSetting("Auth:TokenLifetimes:AccessTokenLifetime", "00:15:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshTokenLifetime", "30.00:00:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshReplayGraceWindow", "00:00:30");

        // A trusted suite origin for the cross-app SSO return_uri allow-list (FR-031, ADR-0039). The
        // factory runs in Production, where loopback is not auto-allowed, so ConnectEndpointsTests
        // asserts against this explicit origin — the same Cors:AllowedOrigins the CORS policy reads.
        builder.UseSetting("Cors:AllowedOrigins:0", "https://sibling.test");

        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(ConstantTimeClock));
    }
}

/// <summary>Shares one API host and database across the account tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class AccountApi : ICollectionFixture<AccountApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "account-api";
}

/// <summary>
/// An account API host of its own for the timing tests, run after every parallel collection has
/// finished and with nothing beside it.
/// </summary>
/// <remarks>
/// Running alone keeps the decoy-equalised routes, which are judged on the ratio of their medians,
/// away from contention by the parallel collections. It was also the first answer to a failure on a
/// constant-time route (122.90 ms against 120.64 ms, see the 2026-10-21 device-test re-check), but
/// that one was timer noise, which isolation does not remove: see <see cref="ConstantTimeEnvelope"/>.
/// </remarks>
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class TimingApi : ICollectionFixture<AccountApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "account-api-timing";
}
