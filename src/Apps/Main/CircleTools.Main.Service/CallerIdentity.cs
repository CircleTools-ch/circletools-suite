// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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

    /// <summary>Gets the caller's display name from the token, falling back when none is present.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>The display name, or a neutral fallback.</returns>
    internal static string DisplayName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // The access token carries the display name under the JWT "name" claim (AccessTokenIssuer),
        // which may or may not be mapped to ClaimTypes.Name depending on inbound claim mapping.
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("name");
        return string.IsNullOrWhiteSpace(name) ? "Someone" : name;
    }
}
