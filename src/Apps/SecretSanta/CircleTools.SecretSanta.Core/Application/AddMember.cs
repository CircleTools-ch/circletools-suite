// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to add a member to a group's shared roster. Wishlists are set per event (ADR-0042).</summary>
/// <param name="GroupId">The group to add to.</param>
/// <param name="Name">A non-empty, group-unique member name.</param>
public sealed record AddMemberCommand(Guid GroupId, string Name) : ICommand<Guid>;

/// <summary>Handles <see cref="AddMemberCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class AddMemberCommandHandler(IGroupRepository repository)
    : ICommandHandler<AddMemberCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(AddMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        var memberId = group.AddMember(command.Name);
        await repository.SaveAsync(group, cancellationToken);
        return memberId;
    }
}
