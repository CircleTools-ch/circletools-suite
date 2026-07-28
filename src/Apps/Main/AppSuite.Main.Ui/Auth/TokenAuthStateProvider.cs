using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;
using AppSuite.ApiClient;
using Microsoft.AspNetCore.Components.Authorization;

namespace AppSuite.Main.Ui.Auth;

/// <summary>
/// Derives the current <see cref="ClaimsPrincipal"/> from the in-memory access token (ADR-0021).
/// </summary>
/// <remarks>
/// <para>
/// The token is the only thing the client knows about the signed-in user, so it is also the only
/// honest source for <c>AuthorizeView</c>. There is no separate "is signed in" flag to drift out of
/// step with it: no token means anonymous, always.
/// </para>
/// <para>
/// <b>The signature is not verified here, deliberately.</b> The client has no key and never should;
/// every request is re-checked by the API, which does have one. Treating the payload as display
/// data — a name to greet, a menu to show — is exactly as much trust as an unverified claim earns.
/// A forged token buys nothing but a misleading UI on the attacker's own screen.
/// </para>
/// <para>
/// This lives in the feature assembly rather than the WebAssembly host on purpose: nothing about it
/// is browser-specific, so a future MAUI head reuses it as-is. FEAT-002 sketches it in the host;
/// that placement is the deviation, and this is the reason for it.
/// </para>
/// </remarks>
/// <param name="tokens">The in-memory access-token store.</param>
public sealed class TokenAuthStateProvider(IAccessTokenStore tokens) : AuthenticationStateProvider
{
    /// <summary>Claim carrying whether the account's email address has been proven (FR-007).</summary>
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>Claim carrying the user's preferred language (ADR-0011).</summary>
    public const string LocaleClaim = "locale";

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(BuildPrincipal()));

    /// <summary>
    /// Re-reads the token and tells the component tree. Called after every sign-in, sign-out and
    /// refresh — the token store is a plain field, so nothing else notices it changed.
    /// </summary>
    public void NotifyChanged() =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal())));

    private ClaimsPrincipal BuildPrincipal()
    {
        var token = tokens.AccessToken;

        if (string.IsNullOrEmpty(token))
        {
            return Anonymous;
        }

        using var payload = ReadPayload(token);

        if (payload is null)
        {
            return Anonymous;
        }

        // "jwt" as the authentication type is what makes IsAuthenticated true; an identity built
        // without one is anonymous no matter how many claims it carries.
        var identity = new ClaimsIdentity("jwt", ClaimTypes.Name, ClaimTypes.Role);

        foreach (var claim in payload.RootElement.EnumerateObject())
        {
            var value = claim.Value.ValueKind switch
            {
                JsonValueKind.String => claim.Value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => claim.Value.GetRawText(),
                _ => null,
            };

            if (value is null)
            {
                continue;
            }

            identity.AddClaim(new Claim(Map(claim.Name), value));
        }

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Maps the JWT's registered claim names onto the <see cref="ClaimTypes"/> URIs that
    /// <c>ClaimsPrincipal.Identity.Name</c> and the framework's helpers look for.
    /// </summary>
    private static string Map(string name) => name switch
    {
        "sub" => ClaimTypes.NameIdentifier,
        "name" => ClaimTypes.Name,
        "email" => ClaimTypes.Email,
        _ => name,
    };

    /// <summary>
    /// Decodes the token's middle segment. Anything malformed yields <see langword="null"/> and is
    /// treated as signed out, which is the only safe reading of a token we cannot understand.
    /// </summary>
    private static JsonDocument? ReadPayload(string token)
    {
        var segments = token.Split('.');

        if (segments.Length != 3)
        {
            return null;
        }

        try
        {
            var bytes = Base64Url.DecodeFromChars(segments[1]);
            return JsonDocument.Parse(bytes);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}

/// <summary>Reads the account facts the UI needs off a <see cref="ClaimsPrincipal"/>.</summary>
public static class AccountClaims
{
    /// <summary>The account's id, or <see cref="Guid.Empty"/> when signed out.</summary>
    /// <param name="principal">The current principal.</param>
    /// <returns>The user's id.</returns>
    public static Guid UserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : Guid.Empty;
    }

    /// <summary>The name to greet the user by; empty when signed out.</summary>
    /// <param name="principal">The current principal.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Whether the address has been proven. A pending account may sign in but must not act on
    /// anything account-scoped (FR-007), which is what the verification banner is about.
    /// </summary>
    /// <param name="principal">The current principal.</param>
    /// <returns><see langword="true"/> once the address is verified.</returns>
    public static bool IsEmailVerified(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return string.Equals(
            principal.FindFirst(TokenAuthStateProvider.EmailVerifiedClaim)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The account's preferred language, or <see langword="null"/> when signed out.</summary>
    /// <param name="principal">The current principal.</param>
    /// <returns>The stored culture name.</returns>
    public static string? Locale(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirst(TokenAuthStateProvider.LocaleClaim)?.Value;
    }

    /// <summary>The address the account signs in with; empty when signed out.</summary>
    /// <param name="principal">The current principal.</param>
    /// <returns>The email address.</returns>
    public static string Email(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    }
}
