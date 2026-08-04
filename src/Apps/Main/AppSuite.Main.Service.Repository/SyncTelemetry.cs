using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace AppSuite.Main.Service.Repository;

/// <summary>
/// The escalation hook M3 ships behind last-write-wins (ADR-0016, ADR-0012). Every time a concurrent
/// edit is discarded in full, this records the loss so the parked conflict-resolution UI has evidence
/// to be parked <em>on</em> — the count of collisions actually happening.
/// </summary>
/// <remarks>
/// This is a real observable signal, never a stub. The M2 lesson (FR-024): a fitness function that
/// exists only as a design comment tells you nothing. A test substitutes a capturing sink and asserts
/// the event fires; the production implementation increments an OpenTelemetry counter and logs.
/// </remarks>
public interface ISyncTelemetry
{
    /// <summary>Records that a last-write-wins discard dropped one aggregate's body in full.</summary>
    /// <param name="kind">The discarded aggregate's kind (<c>group</c> / <c>profile</c>).</param>
    /// <param name="aggregateId">The discarded aggregate's identity.</param>
    void RecordCollision(string kind, Guid aggregateId);
}

/// <summary>
/// The production <see cref="ISyncTelemetry"/>: an OpenTelemetry counter plus a warning-level log,
/// carrying the aggregate kind and id and nothing else — never the discarded body (ADR-0012/0020).
/// </summary>
/// <remarks>
/// The meter name is registered with the host's metrics pipeline in <c>ServiceDefaults</c>, so the
/// counter is exported wherever telemetry goes. Held as a singleton: one meter per process.
/// </remarks>
public sealed class SyncCollisionTelemetry : ISyncTelemetry, IDisposable
{
    /// <summary>The meter name the host registers with OpenTelemetry.</summary>
    public const string MeterName = "AppSuite.Sync";

    private readonly Meter _meter;
    private readonly Counter<long> _collisions;
    private readonly ILogger<SyncCollisionTelemetry> _logger;

    /// <summary>Initializes the meter and its collision counter.</summary>
    /// <param name="logger">The logger the warning is written to.</param>
    public SyncCollisionTelemetry(ILogger<SyncCollisionTelemetry> logger)
    {
        _logger = logger;
        _meter = new Meter(MeterName);
        _collisions = _meter.CreateCounter<long>(
            "appsuite.sync.collisions",
            unit: "{collision}",
            description: "Last-write-wins discards of a concurrently-edited aggregate (FEAT-004).");
    }

    /// <inheritdoc />
    public void RecordCollision(string kind, Guid aggregateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _collisions.Add(1, new KeyValuePair<string, object?>("aggregate.kind", kind));
        _logger.LogWarning(
            "Sync last-write-wins discard: a concurrent edit to {AggregateKind} {AggregateId} was dropped in full.",
            kind,
            aggregateId);
    }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
