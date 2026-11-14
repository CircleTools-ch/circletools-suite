// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to remove a member from a group.</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member to remove.</param>
public sealed record RemoveMemberCommand(Guid GroupId, Guid MemberId) : ICommand;

/// <summary>Handles <see cref="RemoveMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class RemoveMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<RemoveMemberCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.RemoveMember(command.MemberId);
        await repository.SaveAsync(group, cancellationToken);
    }
}
