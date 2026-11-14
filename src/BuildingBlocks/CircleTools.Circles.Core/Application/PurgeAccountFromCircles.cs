// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Application;

/// <summary>
/// Command to remove a departing account from every circle it is in — the second half of what
/// account deletion needs (FR-015).
/// </summary>
/// <param name="UserId">The account being deleted.</param>
/// <remarks>
/// M1 shipped the guard, which answers <em>may this account be deleted</em>. Nothing removed the
/// account's other memberships, and without that a deleted account leaves rows pointing at a user
/// identifier that no longer resolves — a person who is in the people list and is nobody.
/// <para>
/// Managed members are untouched: they are circle data, not their creator's, and they survive the
/// admin who added them (FR-019).
/// </para>
/// </remarks>
public sealed record PurgeAccountFromCirclesCommand(Guid UserId) : ICommand;

/// <summary>Handles <see cref="PurgeAccountFromCirclesCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class PurgeAccountFromCirclesCommandHandler(ICircleRepository repository)
    : ICommandHandler<PurgeAccountFromCirclesCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(PurgeAccountFromCirclesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circles = await repository.ListForUserAsync(command.UserId, cancellationToken);

        foreach (var circle in circles)
        {
            circle.PurgeAccount(command.UserId);
            await repository.SaveAsync(circle, cancellationToken);
        }
    }
}
