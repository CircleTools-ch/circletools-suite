// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository;
using CircleTools.Identity;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Service.Repository;
using CircleTools.SecretSanta.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Count-logic tests for the business-metrics collector (FEAT-022, TP-022): the <see cref="BusinessCountReader"/>
/// must count rows across the three contexts and read <em>events</em> and <em>draws</em> out of the online groups' documents.
/// </summary>
[Collection(BusinessMetricsDatabase.CollectionName)]
public sealed class BusinessMetricsCollectorTests(BusinessMetricsPostgresFixture fixture)
{
    private const int MaxMailAttempts = 5;

    [Fact]
    public async Task ReadAsync_CountsAcrossContexts_AndCountsEventsAndDrawsInTheDocuments()
    {
        await using var provider = fixture.CreateProvider();

        var now = DateTimeOffset.UtcNow;

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            var main = services.GetRequiredService<MainDbContext>();
            var circles = services.GetRequiredService<CirclesDbContext>();
            var secretSanta = services.GetRequiredService<SecretSantaDbContext>();

            // Accounts: two confirmed, one not.
            main.Users.Add(NewUser("a@x.test", confirmed: true));
            main.Users.Add(NewUser("b@x.test", confirmed: true));
            main.Users.Add(NewUser("c@x.test", confirmed: false));
            await main.SaveChangesAsync();

            // One circle with its owner + one managed member => 1 group, 2 memberships.
            var ownerId = Guid.CreateVersion7();
            var created = Circle.Create(ownerId, "Anna", "de-CH", "Müller", "Familie", 0, new CircleLimits(), now);
            Assert.True(created.IsSuccess);
            var circle = created.Value!;
            Assert.True(circle.AddManagedMember(ownerId, "Bruno", "de-CH", new CircleLimits(), now).IsSuccess);
            circles.Circles.Add(circle);
            await circles.SaveChangesAsync();

            // Two online groups: one with two events (drawn twice, and not yet), one with none.
            secretSanta.Groups.Add(NewOnlineGroup(now, """{"events":[{"draws":[{},{}]},{"draws":[]}]}"""));
            secretSanta.Groups.Add(NewOnlineGroup(now, """{"events":[]}"""));
            await secretSanta.SaveChangesAsync();

            // Mail outbox: sent 1 h ago and 23 h ago (in the 24 h window), sent 25 h ago (outside it),
            // a non-mail message sent 1 h ago (not mail), one still being retried, one out of attempts.
            main.OutboxMessages.Add(Sent("mail.verification", now.AddHours(-1)));
            main.OutboxMessages.Add(Sent("mail.member-link", now.AddHours(-23)));
            main.OutboxMessages.Add(Sent("mail.password-reset", now.AddHours(-25)));
            main.OutboxMessages.Add(Sent("other.side-effect", now.AddHours(-1)));
            main.OutboxMessages.Add(Failed("mail.circle-invitation", now, attempts: MaxMailAttempts - 1));
            main.OutboxMessages.Add(Failed("mail.circle-invitation", now, attempts: MaxMailAttempts));
            await main.SaveChangesAsync();
        }

        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var counts = await BusinessCountReader.ReadAsync(
            sp.GetRequiredService<MainDbContext>(),
            sp.GetRequiredService<CirclesDbContext>(),
            sp.GetRequiredService<SecretSantaDbContext>(),
            now,
            MaxMailAttempts,
            CancellationToken.None);

        Assert.Equal(2, counts.AccountsConfirmed);
        Assert.Equal(1, counts.AccountsUnconfirmed);
        Assert.Equal(1, counts.CircleGroups);
        Assert.Equal(2, counts.OnlineGroups);
        Assert.Equal(2, counts.Events);
        Assert.Equal(2, counts.Members);
        Assert.Equal(2, counts.Draws);
        Assert.Equal(2, counts.MailsSentLast24Hours);
        Assert.Equal(1, counts.MailsUndeliverable);
    }

    private static OutboxMessage Sent(string type, DateTimeOffset at)
    {
        var message = OutboxMessage.Create(type, "{}", at);
        message.MarkProcessed(at);
        return message;
    }

    private static OutboxMessage Failed(string type, DateTimeOffset at, int attempts)
    {
        var message = OutboxMessage.Create(type, "{}", at);
        for (var i = 0; i < attempts; i++)
        {
            message.MarkFailed("smtp rejected");
        }

        return message;
    }

    private static CircleToolsUser NewUser(string email, bool confirmed) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = "Test",
        PreferredLanguage = "de-CH",
        EmailConfirmed = confirmed,
        CreatedAt = DateTimeOffset.UtcNow,
        SecurityStamp = Guid.CreateVersion7().ToString(),
    };

    // Only the document's shape matters to the counts; the body is never read back into the app here.
    private static GroupRow NewOnlineGroup(DateTimeOffset now, string body) => new()
    {
        GroupId = Guid.CreateVersion7(),
        Version = 1,
        SchemaVersion = 1,
        Body = body,
        CreatedAt = now,
        UpdatedAt = now,
        LastActivityAt = now,
    };
}

/// <summary>
/// Metric-hygiene test (TP-022): the business meter must emit exactly the seven documented gauges and no
/// tag key outside the closed allow-list — the guard that keeps a name/email/id/pairing from ever
/// becoming a metric dimension (ADR-0012/0020, NFR-004). No database needed.
/// </summary>
public sealed class BusinessMetricsInstrumentTests
{
    private static readonly string[] ExpectedInstruments =
    [
        "circletools.business.accounts",
        "circletools.business.draws",
        "circletools.business.events",
        "circletools.business.groups",
        "circletools.business.mail.sent_last_24h",
        "circletools.business.mail.undeliverable",
        "circletools.business.members",
    ];

    [Fact]
    public void BusinessMetrics_EmitsOnlyTheExpectedGaugesAndTags()
    {
        var measurements = new List<Recorded>();

        using var metrics = new BusinessMetrics();
        metrics.Publish(new BusinessCounts(
            AccountsConfirmed: 2,
            AccountsUnconfirmed: 1,
            CircleGroups: 3,
            OnlineGroups: 4,
            Events: 5,
            Members: 6,
            Draws: 7,
            MailsSentLast24Hours: 8,
            MailsUndeliverable: 9));

        // Filter on this instance's meter, not the meter name: API hosts built by other tests in this
        // assembly run in parallel and register their own "CircleTools.Business" meter, whose gauges would
        // otherwise be recorded too (duplicate measurements → Single() throws, flaky on CI).
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (ReferenceEquals(instrument.Meter, metrics.Meter))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var tagMap = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                tagMap[tag.Key] = tag.Value;
            }

            measurements.Add(new Recorded(instrument.Name, value, tagMap));
        });

        listener.Start();
        listener.RecordObservableInstruments();

        var names = measurements.Select(m => m.Instrument).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedInstruments, names);

        var allowedTagKeys = new HashSet<string>(StringComparer.Ordinal) { "confirmed", "kind" };
        Assert.All(measurements, m => Assert.All(m.Tags.Keys, key => Assert.Contains(key, allowedTagKeys)));

        Assert.Equal(2, Tagged(measurements, "circletools.business.accounts", "confirmed", true));
        Assert.Equal(1, Tagged(measurements, "circletools.business.accounts", "confirmed", false));
        Assert.Equal(3, Tagged(measurements, "circletools.business.groups", "kind", "circle"));
        Assert.Equal(4, Tagged(measurements, "circletools.business.groups", "kind", "online"));
        Assert.Equal(5, Scalar(measurements, "circletools.business.events"));
        Assert.Equal(6, Scalar(measurements, "circletools.business.members"));
        Assert.Equal(7, Scalar(measurements, "circletools.business.draws"));
        Assert.Equal(8, Scalar(measurements, "circletools.business.mail.sent_last_24h"));
        Assert.Equal(9, Scalar(measurements, "circletools.business.mail.undeliverable"));
    }

    private static long Scalar(List<Recorded> measurements, string instrument) =>
        measurements.Single(m => m.Instrument == instrument).Value;

    private static long Tagged(List<Recorded> measurements, string instrument, string tagKey, object tagValue) =>
        measurements.Single(m =>
            m.Instrument == instrument
            && m.Tags.TryGetValue(tagKey, out var value)
            && Equals(value, tagValue)).Value;

    private sealed record Recorded(string Instrument, long Value, Dictionary<string, object?> Tags);
}
