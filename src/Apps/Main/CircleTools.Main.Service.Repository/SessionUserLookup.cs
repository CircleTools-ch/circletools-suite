// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Identity;
using CircleTools.Identity.Tokens;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service.Repository;

/// <summary>EF Core adapter for <see cref="ISessionUserLookup"/>.</summary>
/// <param name="context">The Main database context.</param>
public sealed class SessionUserLookup(MainDbContext context) : ISessionUserLookup
{
    /// <inheritdoc />
    public async Task<CircleToolsUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
}
