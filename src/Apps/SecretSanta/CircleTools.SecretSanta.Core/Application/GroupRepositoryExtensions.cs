// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Convenience helpers over <see cref="IGroupRepository"/> used by the command handlers.</summary>
internal static class GroupRepositoryExtensions
{
    /// <summary>Gets a group by identity or throws when it does not exist.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="groupId">The group identity.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The group.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the group is not found.</exception>
    public static async Task<Group> RequireAsync(
        this IGroupRepository repository, Guid groupId, CancellationToken cancellationToken)
    {
        var group = await repository.GetAsync(groupId, cancellationToken);
        return group ?? throw new InvalidOperationException($"Group '{groupId}' was not found.");
    }
}
