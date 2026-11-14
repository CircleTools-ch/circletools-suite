// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CircleTools.Identity.Tokens;

/// <summary>
/// Mints short-lived JWT access tokens for a signed-in <see cref="CircleToolsUser"/> (ADR-0021).
/// </summary>
/// <param name="jwtOptions">Issuer, audience and signing key.</param>
/// <param name="lifetimes">Configured token lifetimes.</param>
/// <param name="clock">Time source, injected so expiry is testable.</param>
public sealed class AccessTokenIssuer(
    IOptions<JwtOptions> jwtOptions,
    IOptions<TokenLifetimeOptions> lifetimes,
    TimeProvider clock)
{
    /// <summary>Claim carrying whether the account's email is verified.</summary>
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>Claim carrying the user's preferred language (ADR-0011).</summary>
    public const string LocaleClaim = "locale";

    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Issues an access token for a user.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <returns>The encoded token and its expiry.</returns>
    /// <exception cref="InvalidOperationException">The signing key is missing or too short.</exception>
    public AccessToken Issue(CircleToolsUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var options = jwtOptions.Value;
        var keyBytes = Encoding.UTF8.GetBytes(options.SigningKey ?? string.Empty);

        if (keyBytes.Length < JwtOptions.MinimumSigningKeyBytes)
        {
            // Fail loudly rather than sign with a weak key: in a bearer model a forgeable
            // signature means an attacker can mint sessions for any account.
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey must be at least " +
                $"{JwtOptions.MinimumSigningKeyBytes} bytes; it was {keyBytes.Length}.");
        }

        var now = clock.GetUtcNow();
        var expiresAt = now + lifetimes.Value.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
                [JwtRegisteredClaimNames.Name] = user.DisplayName,

                // Endpoints gate account-scoped features on this: FR-007 lets a pending account
                // sign in, but nothing that touches other people may happen on an unproven address.
                [EmailVerifiedClaim] = user.EmailConfirmed,
                [LocaleClaim] = user.PreferredLanguage,
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
