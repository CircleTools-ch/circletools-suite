using AppSuite.Application;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Command to run (or re-run) the draw for a group.</summary>
/// <param name="GroupId">The group to draw.</param>
public sealed record RunDrawCommand(Guid GroupId) : ICommand<RunDrawResultDto>;

/// <summary>Handles <see cref="RunDrawCommand"/> by generating a draw and persisting it on success.</summary>
/// <param name="repository">The group repository port.</param>
/// <param name="generator">The draw generator domain service.</param>
/// <param name="timeProvider">The clock used to stamp the draw.</param>
public sealed class RunDrawCommandHandler(IGroupRepository repository, DrawGenerator generator, TimeProvider timeProvider)
    : ICommandHandler<RunDrawCommand, RunDrawResultDto>
{
    /// <inheritdoc />
    public async Task<RunDrawResultDto> HandleAsync(RunDrawCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var result = group.RunDraw(generator, timeProvider.GetUtcNow());

        if (result.IsSuccess)
        {
            await repository.SaveAsync(group, cancellationToken);
            return new RunDrawResultDto(true, null, result.Assignments.Count);
        }

        return new RunDrawResultDto(false, result.FailureReason?.ToString(), 0);
    }
}
