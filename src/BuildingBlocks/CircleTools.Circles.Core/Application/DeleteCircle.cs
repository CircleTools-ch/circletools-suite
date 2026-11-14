// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Application;

/// <summary>Command to delete a circle (FR-022). Owner only, immediate and irreversible.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle to delete.</param>
/// <param name="ConfirmedName">
/// The circle's name as the owner typed it back. No password: account deletion asks for one because
/// the threat there is a stolen session taking everything, while the threat here is the misclick —
/// and typing a name defends against that without training users to type their password into
/// in-app prompts.
/// </param>
public sealed record DeleteCircleCommand(Guid UserId, Guid CircleId, string ConfirmedName) : ICommand<CircleResult>;

/// <summary>Handles <see cref="DeleteCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <remarks>
/// Deletion takes the circle, every membership, every managed-member profile and every pending
/// invitation, in one transaction. No account is touched, and no Secret-Santa data is: a group
/// created from this circle keeps a circle identifier that now resolves to nothing, which the
/// Secret-Santa side tolerates rather than treating as corruption.
/// </remarks>
public sealed class DeleteCircleCommandHandler(ICircleRepository repository)
    : ICommandHandler<DeleteCircleCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(DeleteCircleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var permission = circle.Authorize(command.UserId, CircleRole.Owner);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        if (!string.Equals(command.ConfirmedName?.Trim(), circle.Name, StringComparison.OrdinalIgnoreCase))
        {
            return CircleResult.Refused(CircleError.NameConfirmationMismatch);
        }

        await repository.DeleteAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}
