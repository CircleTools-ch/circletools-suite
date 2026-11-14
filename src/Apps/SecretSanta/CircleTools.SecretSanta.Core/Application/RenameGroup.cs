// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to rename a group.</summary>
/// <param name="GroupId">The group to rename.</param>
/// <param name="Name">The new, non-empty name.</param>
public sealed record RenameGroupCommand(Guid GroupId, string Name) : ICommand;

/// <summary>Handles <see cref="RenameGroupCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class RenameGroupCommandHandler(IGroupRepository repository)
    : ICommandHandler<RenameGroupCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(RenameGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.Rename(command.Name);
        await repository.SaveAsync(group, cancellationToken);
    }
}
