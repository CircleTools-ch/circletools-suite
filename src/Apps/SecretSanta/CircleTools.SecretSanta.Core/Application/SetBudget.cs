using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to set or clear one event's gift budget.</summary>
/// <param name="GroupId">The group to update.</param>
/// <param name="EventId">The event whose budget is set.</param>
/// <param name="Amount">The budget amount, or <see langword="null"/> to clear the budget.</param>
/// <param name="Currency">The currency code; required when <paramref name="Amount"/> is set.</param>
public sealed record SetBudgetCommand(Guid GroupId, Guid EventId, decimal? Amount, string? Currency) : ICommand;

/// <summary>Handles <see cref="SetBudgetCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class SetBudgetCommandHandler(IGroupRepository repository)
    : ICommandHandler<SetBudgetCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(SetBudgetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var budget = command.Amount is { } amount
            ? Budget.Create(amount, command.Currency ?? string.Empty)
            : null;
        group.SetEventBudget(command.EventId, budget);
        await repository.SaveAsync(group, cancellationToken);
    }
}
