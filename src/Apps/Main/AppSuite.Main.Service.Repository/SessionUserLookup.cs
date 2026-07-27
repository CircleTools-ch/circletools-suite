using AppSuite.Identity;
using AppSuite.Identity.Tokens;
using Microsoft.EntityFrameworkCore;

namespace AppSuite.Main.Service.Repository;

/// <summary>EF Core adapter for <see cref="ISessionUserLookup"/>.</summary>
/// <param name="context">The Main database context.</param>
public sealed class SessionUserLookup(MainDbContext context) : ISessionUserLookup
{
    /// <inheritdoc />
    public async Task<AppSuiteUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
}
