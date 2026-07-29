using AppSuite.Circles.Core.Domain;
using AppSuite.Identity;
using Microsoft.AspNetCore.Identity;

namespace AppSuite.Main.Service;

/// <summary>
/// Implements the Circles context's <see cref="IAccountDirectory"/> port over ASP.NET Core Identity.
/// </summary>
/// <param name="users">The identity user store.</param>
/// <remarks>
/// <para>
/// The one place where Circles and Identity meet, and it lives in the host because neither context
/// may reference the other: a membership holds a user identifier as a <em>value</em>, and a circle
/// survives the deletion of an account it names.
/// </para>
/// <para>
/// <strong>An unverified account resolves to nothing</strong>, in both directions. That is not a
/// convenience: a membership is only ever granted to a proven owner of the invited address (FR-018),
/// and answering with an unverified account here would let someone claim an invitation by
/// registering the address without ever proving they can read it.
/// </para>
/// </remarks>
public sealed class IdentityAccountDirectory(UserManager<AppSuiteUser> users) : IAccountDirectory
{
    /// <inheritdoc />
    public async Task<CircleAccount?> FindByVerifiedEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ToAccount(await users.FindByEmailAsync(email).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<CircleAccount?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ToAccount(await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false));
    }

    private static CircleAccount? ToAccount(AppSuiteUser? user) =>
        user is null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email)
            ? null
            : new CircleAccount(user.Id, user.DisplayName, user.Email, user.PreferredLanguage);
}
