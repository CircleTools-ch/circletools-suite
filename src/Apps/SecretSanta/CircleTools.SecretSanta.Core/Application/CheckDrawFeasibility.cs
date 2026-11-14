// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Whether the group's exclusions still leave a valid draw, and who is left without a recipient or a giver
/// (Q13a) — for the exclusions page to warn at once. <see langword="null"/> when the group is not found.
/// </summary>
/// <param name="GroupId">The group to check.</param>
public sealed record CheckDrawFeasibilityQuery(Guid GroupId) : IQuery<DrawFeasibilityDto?>;

/// <summary>Handles <see cref="CheckDrawFeasibilityQuery"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class CheckDrawFeasibilityQueryHandler(IGroupRepository repository)
    : IQueryHandler<CheckDrawFeasibilityQuery, DrawFeasibilityDto?>
{
    /// <inheritdoc />
    public async Task<DrawFeasibilityDto?> HandleAsync(CheckDrawFeasibilityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await repository.GetAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var result = DrawFeasibility.Check([.. group.Members.Select(m => m.Id)], group.Exclusions);
        string NameOf(Guid id) => group.FindMember(id)?.Name ?? string.Empty;
        return new DrawFeasibilityDto(
            [.. result.WithoutRecipient.Select(NameOf)],
            [.. result.WithoutGiver.Select(NameOf)],
            result.Possible);
    }
}
