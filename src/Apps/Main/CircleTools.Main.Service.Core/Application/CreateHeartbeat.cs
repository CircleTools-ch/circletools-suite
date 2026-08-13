using CircleTools.Application;
using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service.Core.Application;

/// <summary>Command to create a new heartbeat sample.</summary>
/// <param name="Note">A short, non-empty note.</param>
public sealed record CreateHeartbeatCommand(string Note) : ICommand<Guid>;

/// <summary>Handles <see cref="CreateHeartbeatCommand"/> by creating and persisting a heartbeat.</summary>
/// <param name="repository">The heartbeat repository port.</param>
/// <param name="timeProvider">The time source used to stamp creation time.</param>
public sealed class CreateHeartbeatCommandHandler(IHeartbeatRepository repository, TimeProvider timeProvider)
    : ICommandHandler<CreateHeartbeatCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CreateHeartbeatCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var heartbeat = Heartbeat.Create(command.Note, timeProvider.GetUtcNow());
        await repository.AddAsync(heartbeat, cancellationToken);
        return heartbeat.Id;
    }
}
