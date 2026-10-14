using System.Diagnostics.Metrics;
using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository;
using CircleTools.Identity;
using CircleTools.Main.Service.Repository;
using CircleTools.Main.Service.Repository.Linking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Count-logic tests for the business-metrics collector (FEAT-022, TP-022): the <see cref="BusinessCountReader"/>
/// must count rows across the four contexts and derive <em>draws</em> as distinct draw tuples, not rows.
/// </summary>
[Collection(BusinessMetricsDatabase.CollectionName)]
public sealed class BusinessMetricsCollectorTests(BusinessMetricsPostgresFixture fixture)
{
    [Fact]
    public async Task ReadAsync_CountsAcrossContexts_AndDerivesDistinctDraws()
    {
        await using var provider = fixture.CreateProvider();

        var now = DateTimeOffset.UtcNow;

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            var main = services.GetRequiredService<MainDbContext>();
            var circles = services.GetRequiredService<CirclesDbContext>();
            var shared = services.GetRequiredService<SharedGroupDbContext>();
            var linking = services.GetRequiredService<LinkingDbContext>();

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

            // Two shared-by-link groups.
            shared.Groups.Add(NewSharedGroup(now));
            shared.Groups.Add(NewSharedGroup(now));
            await shared.SaveChangesAsync();

            // Two events.
            linking.GroupEvents.Add(NewEvent());
            linking.GroupEvents.Add(NewEvent());
            // Three assignment rows across two distinct (GroupId, EventId, DrawVersion) tuples => draws == 2.
            var groupId = Guid.CreateVersion7();
            var eventId = Guid.CreateVersion7();
            linking.Assignments.Add(NewAssignment(groupId, eventId, giver: Guid.CreateVersion7(), drawVersion: 1));
            linking.Assignments.Add(NewAssignment(groupId, eventId, giver: Guid.CreateVersion7(), drawVersion: 1));
            linking.Assignments.Add(NewAssignment(groupId, eventId, giver: Guid.CreateVersion7(), drawVersion: 2));
            await linking.SaveChangesAsync();
        }

        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var counts = await BusinessCountReader.ReadAsync(
            sp.GetRequiredService<MainDbContext>(),
            sp.GetRequiredService<CirclesDbContext>(),
            sp.GetRequiredService<SharedGroupDbContext>(),
            sp.GetRequiredService<LinkingDbContext>(),
            CancellationToken.None);

        Assert.Equal(2, counts.AccountsConfirmed);
        Assert.Equal(1, counts.AccountsUnconfirmed);
        Assert.Equal(1, counts.CircleGroups);
        Assert.Equal(2, counts.SharedGroups);
        Assert.Equal(2, counts.Events);
        Assert.Equal(2, counts.Members);
        Assert.Equal(2, counts.Draws); // 3 rows → 2 distinct draw tuples
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

    private static SharedGroupRow NewSharedGroup(DateTimeOffset now) => new()
    {
        GroupId = Guid.CreateVersion7(),
        Body = "{}",
        Version = 1,
        ServerModifiedAt = now,
        CreatedAt = now,
        LastActivityAt = now,
    };

    private static GroupEventRow NewEvent() => new()
    {
        GroupId = Guid.CreateVersion7(),
        EventId = Guid.CreateVersion7(),
        OrganizerAccountId = Guid.CreateVersion7(),
        GroupName = "Group",
        EventName = "Event",
    };

    private static AssignmentRow NewAssignment(Guid groupId, Guid eventId, Guid giver, long drawVersion) => new()
    {
        GroupId = groupId,
        EventId = eventId,
        EventName = "Event",
        GiverAccountId = giver,
        OrganizerAccountId = Guid.CreateVersion7(),
        GroupName = "Group",
        RecipientMemberId = Guid.CreateVersion7(),
        RecipientName = "Recipient",
        RecipientWishlistSnapshot = "[]",
        RecipientGiftNote = string.Empty,
        DrawVersion = drawVersion,
    };
}

/// <summary>
/// Metric-hygiene test (TP-022): the business meter must emit exactly the five documented gauges and no
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
            SharedGroups: 4,
            Events: 5,
            Members: 6,
            Draws: 7));

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
        Assert.Equal(4, Tagged(measurements, "circletools.business.groups", "kind", "shared"));
        Assert.Equal(5, Scalar(measurements, "circletools.business.events"));
        Assert.Equal(6, Scalar(measurements, "circletools.business.members"));
        Assert.Equal(7, Scalar(measurements, "circletools.business.draws"));
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
