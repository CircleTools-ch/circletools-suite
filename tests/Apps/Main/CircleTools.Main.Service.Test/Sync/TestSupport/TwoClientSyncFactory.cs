using System.Net.Http.Headers;
using System.Net.Http.Json;
using CircleTools.Circles.Repository;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// Boots the real Main API against a throwaway Postgres for the two-client sync harness (FEAT-004 /
/// TP-004). Two client stores drive <strong>real</strong> sync cycles over this host and the real
/// <c>sync</c> schema, which is the only way convergence — "device B holds device A's change" — can be
/// observed at all (a single change tracker cannot express it).
/// </summary>
/// <remarks>
/// The one production substitution is <see cref="ISyncTelemetry"/>: the collision counter is swapped
/// for a <see cref="CapturingSyncTelemetry"/> so a test can assert a last-write-wins discard actually
/// emitted its event. Everything else — routing, JWT validation, EF, the Postgres store — is the real
/// stack, unchanged.
/// </remarks>
public sealed class TwoClientSyncFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("maindb_sync_tests")
        .Build();

    /// <summary>The capturing collision sink shared by the host — the evidence the LWW hook fired.</summary>
    public CapturingSyncTelemetry Telemetry { get; } = new();

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

    /// <summary>
    /// Registers a fresh account and signs in, returning an authorized client and the account's id —
    /// the two things every device in a harness needs. The account is not email-verified; sync only
    /// requires an authenticated session, not a proven address.
    /// </summary>
    /// <returns>An authorized <see cref="HttpClient"/> for the account and its identity.</returns>
    public async Task<SyncTestAccount> RegisterAccountAsync()
    {
        var email = $"sync-{Guid.CreateVersion7():N}@example.test";
        const string password = "correct-horse-battery";
        var client = CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Sync Tester", email, password, "de-CH"));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        // The client learns its own account id the same way the app does: from the profile it just
        // authenticated to read (there is no id on the session token payload the client parses).
        var profile = (await client.GetFromJsonAsync<ProfileResponse>("/api/v1/account/profile"))!;

        return new SyncTestAccount(this, profile.Id, session.AccessToken);
    }

    /// <summary>Mints another authorized client for the same account — a second device on one login.</summary>
    /// <param name="accessToken">The bearer token the account signed in with.</param>
    /// <returns>An authorized client carrying that token.</returns>
    public HttpClient CreateAuthorizedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development would re-run migrations on startup against the wrong connection string.
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:maindb", _database.GetConnectionString());

        // These tests share one source address and would otherwise trip the NFR-005 rate limit.
        builder.UseSetting("RateLimiting:SensitivePermitLimit", "100000");
        builder.UseSetting("Auth:Jwt:Issuer", "circletools");
        builder.UseSetting("Auth:Jwt:Audience", "circletools-api");
        builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-long-enough-for-hmac");
        builder.UseSetting("Auth:TokenLifetimes:AccessTokenLifetime", "00:15:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshTokenLifetime", "30.00:00:00");
        builder.UseSetting("Auth:TokenLifetimes:RefreshReplayGraceWindow", "00:00:30");

        // Substitute the capturing collision sink for the production meter so a test can prove the
        // last-write-wins hook actually fired (TP-004: wired and observed, not a stub).
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISyncTelemetry>();
            services.AddSingleton<ISyncTelemetry>(Telemetry);
        });
    }
}

/// <summary>Shares one API host and database across the two-client sync tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class TwoClientSyncApi : ICollectionFixture<TwoClientSyncFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "two-client-sync";
}
