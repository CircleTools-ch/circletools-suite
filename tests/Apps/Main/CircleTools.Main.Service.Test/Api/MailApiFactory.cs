using System.Text.Json;
using CircleTools.Circles.Repository;
using CircleTools.Main.Service.Repository;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Boots the API against a real Postgres <em>and</em> a real Mailpit, so mail is asserted as a
/// delivered message rather than as a mock invocation (ADR-0023).
/// </summary>
public sealed class MailApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const int MailpitSmtpPort = 1025;
    private const int MailpitHttpPort = 8025;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("maindb_mail_tests")
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit")
        .WithPortBinding(MailpitSmtpPort, assignRandomHostPort: true)
        .WithPortBinding(MailpitHttpPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(MailpitHttpPort))
        .Build();

    private HttpClient? _mailpitApi;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _mailpit.StartAsync());

        _mailpitApi = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(MailpitHttpPort)}"),
        };

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MainDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CirclesDbContext>().Database.MigrateAsync();
        // The organizer check on the linking publish asks the sync store who holds a group (ADR-0080).
        await scope.ServiceProvider.GetRequiredService<SyncDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CircleTools.SecretSanta.Service.Repository.SecretSantaDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _mailpitApi?.Dispose();
        await Task.WhenAll(_database.DisposeAsync().AsTask(), _mailpit.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:maindb", _database.GetConnectionString());

        // These tests share one source address and would otherwise trip the NFR-005 rate limit.
        // The limiter has its own dedicated coverage in RateLimitingTests.
        builder.UseSetting("RateLimiting:SensitivePermitLimit", "100000");
        // The account-free group routes share one per-IP window; every test here comes from the same address.
        builder.UseSetting("RateLimiting:SharedWritePermitLimit", "100000");

        // Likewise the application-level invitation limits: InvitationTimingTests sends dozens of
        // invitations from one inviter, and repeats one recipient address deliberately. Their real
        // coverage is CircleTools.Circles.Repository.Test.InvitationRateLimiterTests, against the same
        // Postgres counters. PendingInvitationsPerCircle is deliberately left at its default — no
        // test here approaches 50, and leaving it on keeps the per-circle cap honest.
        builder.UseSetting("Circles:Limits:InvitationsPerInviterPerHour", "100000");
        builder.UseSetting("Circles:Limits:InvitationsPerInviterPerDay", "100000");
        builder.UseSetting("Circles:Limits:InvitationsPerRecipientPerDay", "100000");

        // Same reasoning for the creation limits: CircleRoleMatrixTests builds a fresh circle for
        // every permitted row so that no row can depend on another's leftovers, and one owner
        // therefore creates far more than a person would in a day. The limits themselves are covered
        // where they are enforced — Circles.Core.Test's CreateCircle_BeyondTheDailyRate_IsRefused and
        // Create_BeyondTheOwnedCircleCap_IsRefused.
        builder.UseSetting("Circles:Limits:OwnedCirclesPerUser", "100000");
        builder.UseSetting("Circles:Limits:CircleCreationsPerDay", "100000");

        builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-long-enough-for-hmac");
        // Test-only at-rest keys (ADR-0061/0062); real values are host-provided secrets (ADR-0050).
        builder.UseSetting("SharedGroups:Security:TokenEncryptionKey", "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        builder.UseSetting("Circles:Security:EmailHashKey", "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        builder.UseSetting("Mail:Host", _mailpit.Hostname);
        builder.UseSetting("Mail:Port", _mailpit.GetMappedPublicPort(MailpitSmtpPort).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Mail:ClientBaseUrl", "https://app.example.test");

        // Pin the sender the tests assert on, so they do not depend on whichever environment's
        // appsettings this Production-mode host happens to load (production is a real address).
        builder.UseSetting("Mail:FromAddress", "no-reply@circletools.test");

        // The tests drive dispatch explicitly, so the background loop must not race them.
        builder.UseSetting("Outbox:PollInterval", "01:00:00");
    }

    /// <summary>Polls the sink until a message addressed to <paramref name="recipient"/> arrives.</summary>
    /// <param name="recipient">The expected recipient address.</param>
    /// <param name="subjectContains">Optional subject filter, when an address receives several mails.</param>
    /// <param name="attempts">How many times to poll before giving up.</param>
    /// <returns>The Mailpit message summary.</returns>
    public async Task<JsonElement> WaitForMailAsync(
        string recipient,
        string? subjectContains = null,
        int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var response = await _mailpitApi!.GetStringAsync(new Uri("/api/v1/messages?limit=200", UriKind.Relative));
            var messages = JsonDocument.Parse(response).RootElement.GetProperty("messages");

            foreach (var message in messages.EnumerateArray())
            {
                if (subjectContains is not null
                    && !(message.GetProperty("Subject").GetString() ?? string.Empty)
                        .Contains(subjectContains, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var to in message.GetProperty("To").EnumerateArray())
                {
                    if (string.Equals(to.GetProperty("Address").GetString(), recipient, StringComparison.OrdinalIgnoreCase))
                    {
                        return message.Clone();
                    }
                }
            }

            await Task.Delay(250);
        }

        throw new InvalidOperationException($"No mail delivered to {recipient} within the timeout.");
    }

    /// <summary>Reads a delivered message's plain-text body.</summary>
    /// <param name="messageId">The Mailpit message id.</param>
    /// <returns>The body text.</returns>
    public async Task<string> ReadBodyAsync(string messageId)
    {
        var json = await _mailpitApi!.GetStringAsync(new Uri($"/api/v1/message/{messageId}", UriKind.Relative));
        return JsonDocument.Parse(json).RootElement.GetProperty("Text").GetString() ?? string.Empty;
    }
}

/// <summary>Shares one API host, database and mail sink across the delivery tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class MailApi : ICollectionFixture<MailApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "mail-api";
}
