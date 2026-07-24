using AppSuite.Application;
using AppSuite.SecretSanta.Core.Domain;

namespace AppSuite.SecretSanta.Core.Application;

/// <summary>Command to set or clear a group's gift budget.</summary>
/// <param name="GroupId">The group to update.</param>
/// <param name="Amount">The budget amount, or <see langword="null"/> to clear the budget.</param>
/// <param name="Currency">The currency code; required when <paramref name="Amount"/> is set.</param>
public sealed record SetBudgetCommand(Guid GroupId, decimal? Amount, string? Currency) : ICommand;

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
        group.SetBudget(budget);
        await repository.SaveAsync(group, cancellationToken);
    }
}
