using System.Net;
using System.Net.Http.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Boots the API with a deliberately tiny rate limit, so the limiter can be exercised without the
/// rest of the suite fighting it. Constructed per test through <see cref="CreateAsync"/> so each
/// test owns a fresh limiter window (see <see cref="RateLimitingTests"/>).
/// </summary>
public sealed class RateLimitedApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Requests permitted per window in these tests.</summary>
    public const int PermitLimit = 3;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("maindb_ratelimit_tests")
        .Build();

    /// <summary>Starts and migrates the database, and returns a ready-to-use factory.</summary>
    public static async Task<RateLimitedApiFactory> CreateAsync()
    {
        var factory = new RateLimitedApiFactory();
        await factory._database.StartAsync();

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MainDbContext>().Database.MigrateAsync();
        return factory;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:maindb", _database.GetConnectionString());
        builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-long-enough-for-hmac");
        builder.UseSetting(
            "RateLimiting:SensitivePermitLimit",
            PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Outbox:PollInterval", "01:00:00");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>Covers the rate-limiting half of NFR-005.</summary>
/// <remarks>
/// Each test creates its own factory (via <see cref="RateLimitedApiFactory.CreateAsync"/>), so it
/// owns a fresh server and a fresh in-process fixed-window limiter. A single shared factory would
/// not work: the limiter partitions on the connection's remote IP (<c>Program.cs</c>), which is
/// absent under the in-memory test server, so every request falls in one <c>"unknown"</c> partition
/// within the same one-minute window — whichever test ran first would spend the tiny permit budget
/// the next test asserts against, making the suite order-dependent.
/// </remarks>
public sealed class RateLimitingTests
{
    [Fact]
    public async Task SensitiveEndpoints_AreRateLimitedPerSource()
    {
        await using var factory = await RateLimitedApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/account/forgot-password",
                new ForgotPasswordRequest($"nobody-{i}@example.test"));
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(RateLimitedApiFactory.PermitLimit, statuses.Count(s => s != HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task RateLimitRejection_DoesNotRevealWhetherTheAccountExists()
    {
        // A lockout message that only appears for real accounts is itself an enumeration oracle,
        // so the 429 must be byte-identical for a registered and an unregistered address.
        await using var factory = await RateLimitedApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var registered = $"known-{Guid.CreateVersion7():N}@example.test";

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", registered, "correct-horse-battery", "en"));

        // Burn through the window.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 2; i++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/account/forgot-password",
                new ForgotPasswordRequest($"filler-{i}@example.test"));
        }

        var knownResponse = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password", new ForgotPasswordRequest(registered));
        var unknownResponse = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password", new ForgotPasswordRequest("nobody@example.test"));

        Assert.Equal(HttpStatusCode.TooManyRequests, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, unknownResponse.StatusCode);
        Assert.Equal(
            await knownResponse.Content.ReadAsStringAsync(),
            await unknownResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_IsNotRateLimited()
    {
        // Refresh is not an address-accepting endpoint, and every signed-in tab calls it on a
        // timer. Throttling it by source address would break households behind one NAT address.
        await using var factory = await RateLimitedApiFactory.CreateAsync();
        using var client = factory.CreateClient();

        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/account/refresh",
                new RefreshRequest("not-a-real-token"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
