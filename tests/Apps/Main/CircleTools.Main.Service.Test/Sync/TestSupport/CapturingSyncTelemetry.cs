// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using CircleTools.Main.Service.Repository;

namespace CircleTools.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// A capturing <see cref="ISyncTelemetry"/> the two-client harness substitutes for the production
/// meter (FEAT-004 / TP-004). The M2 lesson carried into FR-024 is that a hook designed but never
/// observed is worthless; this sink lets a test assert that a last-write-wins discard actually emits
/// its collision event, with the aggregate kind and id, rather than trusting a design comment.
/// </summary>
public sealed class CapturingSyncTelemetry : ISyncTelemetry
{
    private readonly ConcurrentQueue<(string Kind, Guid AggregateId)> _collisions = new();

    /// <summary>Every collision recorded since the host started, in the order they were emitted.</summary>
    public IReadOnlyList<(string Kind, Guid AggregateId)> Collisions => _collisions.ToArray();

    /// <inheritdoc />
    public void RecordCollision(string kind, Guid aggregateId) => _collisions.Enqueue((kind, aggregateId));
}
