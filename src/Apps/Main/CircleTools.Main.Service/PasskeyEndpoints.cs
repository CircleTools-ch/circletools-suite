using System.Security.Claims;
using System.Text.Json;
using CircleTools.Identity;
using CircleTools.Identity.Tokens;
using CircleTools.Main.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CircleTools.Main.Service;

/// <summary>
/// Passkey registration and sign-in (FR-010, FR-011), on ASP.NET Core Identity's built-in
/// WebAuthn support (ADR-0009).
/// </summary>
/// <remarks>
/// The browser performs the ceremony; these endpoints only hand it the options and verify what
/// comes back. The relying-party id is <c>IdentityPasskeyOptions.ServerDomain</c>, configured to
/// the registrable suite domain so one passkey works across the shell and every app subdomain.
/// </remarks>
public static class PasskeyEndpoints
{
    /// <summary>Maps the passkey endpoints onto the account route group.</summary>
    /// <param name="group">The <c>/api/v{version}/account</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapPasskeyEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/passkeys/options", CreationOptionsAsync)
            .WithName("PasskeyCreationOptions").RequireAuthorization();
        group.MapPost("/passkeys", RegisterPasskeyAsync)
            .WithName("RegisterPasskey").RequireAuthorization();
        group.MapGet("/passkeys", ListPasskeysAsync)
            .WithName("ListPasskeys").RequireAuthorization();
        group.MapDelete("/passkeys/{credentialId}", RemovePasskeyAsync)
            .WithName("RemovePasskey").RequireAuthorization();

        group.MapPost("/login/passkey/options", RequestOptionsAsync)
            .WithName("PasskeyRequestOptions").AllowAnonymous()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapPost("/login/passkey", PasskeyLoginAsync)
            .WithName("PasskeyLogin").AllowAnonymous()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);

        return group;
    }

    /// <summary>Returns options for <c>navigator.credentials.create()</c> (FR-010).</summary>
    private static async Task<IResult> CreationOptionsAsync(
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        SignInManager<CircleToolsUser> signIn)
    {
        var user = await FindCallerAsync(principal, users).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var options = await signIn.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.Email ?? user.Id.ToString(),
            DisplayName = user.DisplayName,
        }).ConfigureAwait(false);

        return Results.Content(options.ToString(), "application/json");
    }

    /// <summary>Stores a newly created passkey (FR-010).</summary>
    /// <remarks>
    /// Only the public key and metadata are persisted — the private key never leaves the device,
    /// and no biometric data is transmitted at all.
    /// </remarks>
    private static async Task<IResult> RegisterPasskeyAsync(
        [FromBody] RegisterPasskeyRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        SignInManager<CircleToolsUser> signIn)
    {
        var user = await FindCallerAsync(principal, users).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        PasskeyAttestationResult attestation;

        try
        {
            attestation = await signIn.PerformPasskeyAttestationAsync(request.CredentialJson ?? string.Empty)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PasskeyException or JsonException or InvalidOperationException)
        {
            // As above: malformed input, a rejected credential, or no ceremony underway. All are
            // the caller's problem, not a server error.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["passkey"] = ["The passkey could not be registered."],
            });
        }

        if (!attestation.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["passkey"] = [attestation.Failure?.Message ?? "The passkey could not be registered."],
            });
        }

        var passkey = attestation.Passkey;

        // A label the user recognises later; defaulted from the platform when they skip it.
        passkey.Name = string.IsNullOrWhiteSpace(request.Label) ? "Passkey" : request.Label.Trim();

        var stored = await users.AddOrUpdatePasskeyAsync(user, passkey).ConfigureAwait(false);

        return stored.Succeeded
            ? Results.Ok(ToDto(passkey))
            : Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Could not store the passkey.");
    }

    /// <summary>Lists the caller's passkeys (FR-010).</summary>
    private static async Task<IResult> ListPasskeysAsync(
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users)
    {
        var user = await FindCallerAsync(principal, users).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var passkeys = await users.GetPasskeysAsync(user).ConfigureAwait(false);

        return Results.Ok(passkeys.Select(ToDto).ToList());
    }

    /// <summary>
    /// Removes a passkey (FR-010). The password credential always remains, so removing the last
    /// passkey can never lock anyone out.
    /// </summary>
    private static async Task<IResult> RemovePasskeyAsync(
        string credentialId,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users)
    {
        var user = await FindCallerAsync(principal, users).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        byte[] id;

        try
        {
            id = WebEncoders.Base64UrlDecode(credentialId);
        }
        catch (FormatException)
        {
            return Results.BadRequest();
        }

        // Scoped to the caller's own passkeys, so one user cannot remove another's credential.
        var existing = await users.GetPasskeyAsync(user, id).ConfigureAwait(false);

        if (existing is null)
        {
            return Results.NotFound();
        }

        var removed = await users.RemovePasskeyAsync(user, id).ConfigureAwait(false);

        return removed.Succeeded ? Results.NoContent() : Results.BadRequest();
    }

    /// <summary>Returns options for <c>navigator.credentials.get()</c> (FR-011).</summary>
    private static async Task<IResult> RequestOptionsAsync(SignInManager<CircleToolsUser> signIn)
    {
        // No user is named: the browser offers whichever discoverable credential it holds for this
        // relying party, so the endpoint reveals nothing about which addresses are registered.
        var options = await signIn.MakePasskeyRequestOptionsAsync(user: null).ConfigureAwait(false);

        return Results.Content(options.ToString(), "application/json");
    }

    /// <summary>Signs in with a passkey, issuing the same session as a password login (FR-011).</summary>
    private static async Task<IResult> PasskeyLoginAsync(
        [FromBody] PasskeyLoginRequest request,
        SignInManager<CircleToolsUser> signIn,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        PasskeyAssertionResult<CircleToolsUser> assertion;

        try
        {
            assertion = await signIn.PerformPasskeyAssertionAsync(request.CredentialJson ?? string.Empty)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PasskeyException or JsonException or InvalidOperationException)
        {
            // Three client-side situations all throw rather than returning a failed result:
            // malformed JSON, a rejected credential, and posting an assertion when no ceremony is
            // underway (no options were requested first). None is a server fault, and all must
            // answer exactly as a well-formed but wrong credential does — a 500 would leak that
            // the input differed structurally and hand an attacker a probe (NFR-005).
            return InvalidPasskey();
        }

        if (!assertion.Succeeded || assertion.User is null)
        {
            return InvalidPasskey();
        }

        // Deliberately not SignInManager.PasskeySignInAsync: that establishes a cookie session,
        // and this suite is bearer-token only (ADR-0021). The session must be indistinguishable
        // from the one password sign-in produces.
        var tokens = await sessions.StartAsync(assertion.User, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new SessionResponse(
            tokens.AccessToken,
            (int)(tokens.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            tokens.RefreshSecret,
            assertion.User.EmailConfirmed));
    }

    private static async Task<CircleToolsUser?> FindCallerAsync(
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            : null;
    }

    private static IResult InvalidPasskey() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");

    private static PasskeyResponse ToDto(UserPasskeyInfo passkey) =>
        new(WebEncoders.Base64UrlEncode(passkey.CredentialId), passkey.Name, passkey.CreatedAt);
}
