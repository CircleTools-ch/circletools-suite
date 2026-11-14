// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Port for persisting <see cref="Group"/> aggregates. Implemented by adapters outside the domain
/// (e.g. the local browser/file key-value store). The MVP is local-first with no backend.
/// </summary>
public interface IGroupRepository
{
    /// <summary>Lists all stored groups.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the list of groups.</returns>
    Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets a group by identity, or <see langword="null"/> if not found.</summary>
    /// <param name="id">The group identity.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the group, or <see langword="null"/>.</returns>
    Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Inserts or updates a group.</summary>
    /// <param name="group">The group to persist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the group has been saved.</returns>
    Task SaveAsync(Group group, CancellationToken cancellationToken);

    /// <summary>Deletes a group by identity, if present.</summary>
    /// <param name="id">The group identity.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the group has been removed.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
