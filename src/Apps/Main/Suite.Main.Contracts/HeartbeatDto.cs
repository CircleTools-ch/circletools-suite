namespace Suite.Main.Contracts;

/// <summary>Data transfer object describing a heartbeat sample.</summary>
/// <param name="Id">The unique identifier of the heartbeat.</param>
/// <param name="CreatedAt">The instant the heartbeat was created.</param>
/// <param name="Note">A short free-text note.</param>
public sealed record HeartbeatDto(Guid Id, DateTimeOffset CreatedAt, string Note);

/// <summary>Request payload to create a new heartbeat.</summary>
/// <param name="Note">A short free-text note (must not be empty).</param>
public sealed record CreateHeartbeatRequest(string Note);
