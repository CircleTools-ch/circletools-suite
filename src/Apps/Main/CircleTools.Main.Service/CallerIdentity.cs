using System.Security.Claims;

namespace CircleTools.Main.Service;

/// <summary>Reads the calling account's identity out of the bearer token's claims.</summary>
internal static class CallerIdentity
{
    /// <summary>Gets the calling account's identifier.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="userId">The account, when the principal carries one.</param>
    /// <returns><see langword="true"/> when a usable identifier was present.</returns>
    internal static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
