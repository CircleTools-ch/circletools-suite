namespace Suite.Main.Service.Core.Domain;

/// <summary>
/// Port for persisting <see cref="Heartbeat"/> aggregates. Implemented by adapters in the
/// infrastructure layer (e.g. the Postgres repository).
/// </summary>
public interface IHeartbeatRepository
{
    /// <summary>Adds a heartbeat to the store.</summary>
    /// <param name="heartbeat">The heartbeat to add.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the heartbeat has been persisted.</returns>
    Task AddAsync(Heartbeat heartbeat, CancellationToken cancellationToken);

    /// <summary>Lists all heartbeats, most recent first.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the list of heartbeats.</returns>
    Task<IReadOnlyList<Heartbeat>> ListAsync(CancellationToken cancellationToken);
}
