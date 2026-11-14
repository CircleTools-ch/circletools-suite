// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to edit a member's name. Wishlists are per event and edited there (ADR-0042).</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member to edit.</param>
/// <param name="Name">The new, non-empty, group-unique name.</param>
public sealed record EditMemberCommand(Guid GroupId, Guid MemberId, string Name) : ICommand;

/// <summary>Handles <see cref="EditMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class EditMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<EditMemberCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(EditMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.EditMember(command.MemberId, command.Name);
        await repository.SaveAsync(group, cancellationToken);
    }
}
