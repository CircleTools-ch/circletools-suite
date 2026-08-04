using AppSuite.Circles.Repository;
using AppSuite.Main.Service.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Boots the real API against a throwaway Postgres, so the account endpoints are exercised through
/// the whole stack — routing, model binding, Identity, EF and JWT validation.
/// </summary>
public sealed class AccountApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("maindb_api_tests")
        .Build();

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
        builder.UseSetting("Auth:Jwt:Issuer", "appsuite");
        builder.UseSetting("Auth:Jwt:Audience", "appsuite-api");
        builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-long-enough-for-hmac");
        builder.UseSetting("Auth:TokenLifetimes:AccessTokenLifetime", "00:15:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshTokenLifetime", "30.00:00:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshReplayGraceWindow", "00:00:30");
    }
}

/// <summary>Shares one API host and database across the account tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class AccountApi : ICollectionFixture<AccountApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "account-api";
}
