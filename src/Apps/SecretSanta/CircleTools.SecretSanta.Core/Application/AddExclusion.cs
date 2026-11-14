// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to add an exclusion between two members (replacing any existing rule for the pair).</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="GiverMemberId">The giver member identity.</param>
/// <param name="RecipientMemberId">The forbidden recipient member identity.</param>
/// <param name="Mutual"><see langword="true"/> to forbid both directions; <see langword="false"/> for one-way only.</param>
public sealed record AddExclusionCommand(Guid GroupId, Guid GiverMemberId, Guid RecipientMemberId, bool Mutual = false) : ICommand;

/// <summary>Handles <see cref="AddExclusionCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class AddExclusionCommandHandler(IGroupRepository repository)
    : ICommandHandler<AddExclusionCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(AddExclusionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.AddExclusion(command.GiverMemberId, command.RecipientMemberId, command.Mutual);
        await repository.SaveAsync(group, cancellationToken);
    }
}
