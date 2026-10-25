using System.Diagnostics.Metrics;
using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository;
using CircleTools.Main.Service.Repository;
using CircleTools.SecretSanta.Service.Repository;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service;

/// <summary>
/// A snapshot of the suite's headline business counts (FEAT-022). An immutable record so it can be
/// swapped into the metric holder's single <c>volatile</c> field atomically — the gauge callbacks read
/// one consistent set of numbers without a lock.
/// </summary>
/// <param name="AccountsConfirmed">Accounts with a confirmed email (<c>AspNetUsers</c>).</param>
/// <param name="AccountsUnconfirmed">Accounts whose email is not yet confirmed.</param>
/// <param name="CircleGroups">Rows in <c>circles.circles</c>.</param>
/// <param name="OnlineGroups">Rows in <c>secretsanta.groups</c>: Secret-Santa groups kept online (ADR-0086).</param>
/// <param name="Events">Events in the online groups' documents (ADR-0042).</param>
/// <param name="Members">Rows in <c>circles.circle_memberships</c>.</param>
/// <param name="Draws">
/// Draws in the online groups' documents. Groups kept on a device never reach the server, so their draws are
/// not counted (ADR-0086).
/// </param>
internal sealed record BusinessCounts(
    long AccountsConfirmed,
    long AccountsUnconfirmed,
    long CircleGroups,
    long OnlineGroups,
    long Events,
    long Members,
    long Draws)
{
    /// <summary>All-zero counts, published before the first collection completes.</summary>
    public static readonly BusinessCounts Empty = new(0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Counts the business rows across the read stores. Split out of the collector so the query logic is
/// directly integration-testable against a real Postgres, independent of the timer/hosted-service plumbing.
/// </summary>
internal static class BusinessCountReader
{
    // Events and draws live in each group's document (jsonb): count the array elements in the database, so no
    // document — names, wishes, pairings — ever leaves Postgres for this.
    private const string EventCountSql =
        $"""
        SELECT COALESCE(SUM(jsonb_array_length(COALESCE(g."Body"->'events', '[]'::jsonb))), 0)::bigint AS "Value"
        FROM {SecretSantaDbContext.Schema}.groups g
        """;

    private const string DrawCountSql =
        $"""
        SELECT COUNT(*)::bigint AS "Value"
        FROM {SecretSantaDbContext.Schema}.groups g
        CROSS JOIN LATERAL jsonb_array_elements(COALESCE(g."Body"->'events', '[]'::jsonb)) e
        CROSS JOIN LATERAL jsonb_array_elements(COALESCE(e->'draws', '[]'::jsonb)) d
        """;

    /// <summary>
    /// Runs the count queries over the three contexts the counts come from. Caller owns the contexts' lifetime (a
    /// DI scope). Only counts are read — never a name, email, body, or giver→recipient pairing (ADR-0012/0020,
    /// NFR-004).
    /// </summary>
    public static async Task<BusinessCounts> ReadAsync(
        MainDbContext main,
        CirclesDbContext circles,
        SecretSantaDbContext secretSanta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(circles);
        ArgumentNullException.ThrowIfNull(secretSanta);

        var accountsConfirmed = await main.Users.CountAsync(u => u.EmailConfirmed, cancellationToken).ConfigureAwait(false);
        var accountsUnconfirmed = await main.Users.CountAsync(u => !u.EmailConfirmed, cancellationToken).ConfigureAwait(false);
        var circleGroups = await circles.Circles.CountAsync(cancellationToken).ConfigureAwait(false);
        var onlineGroups = await secretSanta.Groups.CountAsync(cancellationToken).ConfigureAwait(false);
        var members = await circles.Set<CircleMembership>().CountAsync(cancellationToken).ConfigureAwait(false);
        var events = await secretSanta.Database.SqlQueryRaw<long>(EventCountSql).SingleAsync(cancellationToken).ConfigureAwait(false);
        var draws = await secretSanta.Database.SqlQueryRaw<long>(DrawCountSql).SingleAsync(cancellationToken).ConfigureAwait(false);

        return new BusinessCounts(
            accountsConfirmed,
            accountsUnconfirmed,
            circleGroups,
            onlineGroups,
            events,
            members,
            draws);
    }
}

/// <summary>
/// Owns the <c>CircleTools.Business</c> meter and its observable gauges (FEAT-022). Gauge callbacks read
/// the latest published <see cref="BusinessCounts"/> from one <c>volatile</c> field — no I/O on the
/// Prometheus scrape thread. Only counts and a closed, low-cardinality tag set (<c>confirmed</c>,
/// <c>kind</c>) are emitted (ADR-0012/0020).
/// </summary>
internal sealed class BusinessMetrics : IDisposable
{
    /// <summary>The meter name registered with OpenTelemetry in <c>ServiceDefaults</c>.</summary>
    public const string MeterName = "CircleTools.Business";

    private readonly Meter _meter;
    private volatile BusinessCounts _counts = BusinessCounts.Empty;

    /// <summary>
    /// This instance's meter. Every instance shares <see cref="MeterName"/>, so a listener that must observe
    /// only one instance (e.g. a test while API hosts run in parallel) filters on this reference, not the name.
    /// </summary>
    internal Meter Meter => _meter;

    /// <summary>Creates the meter and registers the gauges.</summary>
    public BusinessMetrics()
    {
        _meter = new Meter(MeterName);

        _meter.CreateObservableGauge(
            "circletools.business.accounts",
            () => new[]
            {
                new Measurement<long>(_counts.AccountsConfirmed, new KeyValuePair<string, object?>("confirmed", true)),
                new Measurement<long>(_counts.AccountsUnconfirmed, new KeyValuePair<string, object?>("confirmed", false)),
            },
            unit: "{account}",
            description: "Accounts, split by whether the email is confirmed.");

        _meter.CreateObservableGauge(
            "circletools.business.groups",
            () => new[]
            {
                new Measurement<long>(_counts.CircleGroups, new KeyValuePair<string, object?>("kind", "circle")),
                new Measurement<long>(_counts.OnlineGroups, new KeyValuePair<string, object?>("kind", "online")),
            },
            unit: "{group}",
            description: "Groups by server store: circle (circles.circles) and online Secret-Santa groups (secretsanta.groups).");

        _meter.CreateObservableGauge(
            "circletools.business.events",
            () => _counts.Events,
            unit: "{event}",
            description: "Events/occasions in online Secret-Santa groups.");

        _meter.CreateObservableGauge(
            "circletools.business.members",
            () => _counts.Members,
            unit: "{member}",
            description: "Circle memberships (circles.circle_memberships).");

        _meter.CreateObservableGauge(
            "circletools.business.draws",
            () => _counts.Draws,
            unit: "{draw}",
            description: "Draws in online Secret-Santa groups; groups kept on a device are not counted (ADR-0086).");
    }

    /// <summary>Publishes a freshly-collected snapshot for the gauges to report.</summary>
    public void Publish(BusinessCounts counts) => _counts = counts;

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}

/// <summary>
/// Refreshes the <see cref="BusinessMetrics"/> snapshot on a timer (FEAT-022, ADR-0051). Lives in the host
/// because it is the only project that can resolve all three counted EF contexts; the Repository project
/// does not reference Circles.
/// </summary>
/// <remarks>
/// Collection runs off the scrape path (an observable-gauge callback is synchronous and must not touch the
/// database) and is <strong>best-effort</strong>: a transient database error is logged and the previous
/// snapshot kept, never crashing the host.
/// </remarks>
internal sealed class BusinessMetricsCollector : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BusinessMetrics _metrics;
    private readonly ILogger<BusinessMetricsCollector> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    /// <summary>Reads the refresh configuration and captures the collaborators.</summary>
    /// <param name="scopeFactory">Opens a DI scope per collection pass to resolve the EF contexts.</param>
    /// <param name="metrics">The meter holder whose snapshot this collector refreshes.</param>
    /// <param name="configuration">Reads <c>Observability:BusinessMetrics:*</c> (enabled, interval).</param>
    /// <param name="logger">Records a best-effort collection failure.</param>
    public BusinessMetricsCollector(
        IServiceScopeFactory scopeFactory,
        BusinessMetrics metrics,
        IConfiguration configuration,
        ILogger<BusinessMetricsCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
        _enabled = configuration.GetValue("Observability:BusinessMetrics:Enabled", true);
        var seconds = configuration.GetValue("Observability:BusinessMetrics:RefreshIntervalSeconds", 60);
        _interval = TimeSpan.FromSeconds(Math.Max(5, seconds));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            return;
        }

        await CollectAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CollectAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one best-effort collection pass: scope, count, publish.</summary>
    private async Task CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            var counts = await BusinessCountReader.ReadAsync(
                services.GetRequiredService<MainDbContext>(),
                services.GetRequiredService<CirclesDbContext>(),
                services.GetRequiredService<SecretSantaDbContext>(),
                cancellationToken).ConfigureAwait(false);

            _metrics.Publish(counts);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Best-effort telemetry must never bring the host down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Business-metrics collection failed; keeping the previous snapshot.");
        }
    }
}
