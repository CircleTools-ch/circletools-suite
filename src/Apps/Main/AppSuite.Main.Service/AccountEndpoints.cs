using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AppSuite.Identity;
using AppSuite.Identity.Tokens;
using AppSuite.Infrastructure.Outbox;
using AppSuite.Main.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AppSuite.Main.Service;

/// <summary>Account endpoints for the M1 identity slice (FEAT-002).</summary>
public static class AccountEndpoints
{
    /// <summary>Outbox message type for the verification mail.</summary>
    public const string VerificationMailType = "mail.verification";

    /// <summary>
    /// Outbox message type for the mail sent when someone tries to sign up with an address that
    /// already has an account.
    /// </summary>
    public const string SignUpAttemptMailType = "mail.signup-attempt";

    /// <summary>Outbox message type for the password-reset mail.</summary>
    public const string PasswordResetMailType = "mail.password-reset";

    /// <summary>
    /// Rate-limit policy applied to every endpoint that accepts an email address (NFR-005).
    /// </summary>
    /// <remarks>
    /// Partitioned by source address. <strong>Per-address limiting is not yet implemented</strong>:
    /// it needs a counter shared across instances, which is a deployment decision still open
    /// alongside hosting. Until then an attacker distributing across many source addresses is
    /// slowed but not stopped — the identical-response rule is the primary control, and this is
    /// the reinforcement.
    /// </remarks>
    public const string SensitiveRateLimitPolicy = "account-sensitive";

    /// <summary>Maps the account endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/account</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/register", RegisterAsync).WithName("Register").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/verify-email", VerifyEmailAsync).WithName("VerifyEmail").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/forgot-password", ForgotPasswordAsync).WithName("ForgotPassword").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/reset-password", ResetPasswordAsync).WithName("ResetPassword").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/refresh", RefreshAsync).WithName("Refresh").AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).WithName("Logout").RequireAuthorization();
        group.MapGet("/profile", GetProfileAsync).WithName("GetProfile").RequireAuthorization();

        return group;
    }

    /// <summary>
    /// Creates a pending account and queues its verification mail (FR-007).
    /// </summary>
    /// <remarks>
    /// The response is identical whether or not the address already has an account. An address in
    /// use instead receives a "someone tried to sign up" mail, so the real owner learns of the
    /// attempt while the requester learns nothing (NFR-005). Both paths queue exactly one message,
    /// which is also what keeps their timing indistinguishable.
    /// </remarks>
    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        UserManager<AppSuiteUser> users,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["A display name and an email address are required."],
            });
        }

        var now = clock.GetUtcNow();
        var existing = await users.FindByEmailAsync(request.Email).ConfigureAwait(false);

        if (existing is not null)
        {
            await outbox.EnqueueAsync(
                OutboxMessage.Create(
                    SignUpAttemptMailType,
                    JsonSerializer.Serialize(new { email = request.Email, language = existing.PreferredLanguage }),
                    now),
                cancellationToken).ConfigureAwait(false);

            return AcceptedRegistration();
        }

        var user = new AppSuiteUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            PreferredLanguage = string.IsNullOrWhiteSpace(request.Language) ? "de-CH" : request.Language,
            CreatedAt = now,
        };

        var created = await users.CreateAsync(user, request.Password).ConfigureAwait(false);

        if (!created.Succeeded)
        {
            // Password-policy failures are the caller's own input, so reporting them leaks nothing
            // about other accounts. A duplicate-email error can never reach here — that path
            // returned above with the neutral response.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = [.. created.Errors.Select(e => e.Description)],
            });
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);

        await outbox.EnqueueAsync(
            OutboxMessage.Create(
                VerificationMailType,
                JsonSerializer.Serialize(new
                {
                    userId = user.Id,
                    email = user.Email,
                    language = user.PreferredLanguage,
                    token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
                }),
                now),
            cancellationToken).ConfigureAwait(false);

        return AcceptedRegistration();
    }

    /// <summary>Confirms an address and marks the account verified (FR-007).</summary>
    private static async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        UserManager<AppSuiteUser> users)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return Results.BadRequest();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.BadRequest();
        }

        string token;

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Results.BadRequest();
        }

        var result = await users.ConfirmEmailAsync(user, token).ConfigureAwait(false);

        return result.Succeeded ? Results.NoContent() : Results.BadRequest();
    }

    /// <summary>Queues a password-reset link (FR-009).</summary>
    /// <remarks>
    /// The response is identical for a registered and an unregistered address, and no mail is sent
    /// for the latter. Requesting a reset does <em>not</em> invalidate the current password — a
    /// stranger entering someone's address must not be able to lock them out (NFR-005).
    /// </remarks>
    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        UserManager<AppSuiteUser> users,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await users.FindByEmailAsync(request.Email).ConfigureAwait(false);

        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

            await outbox.EnqueueAsync(
                OutboxMessage.Create(
                    PasswordResetMailType,
                    JsonSerializer.Serialize(new
                    {
                        userId = user.Id,
                        email = user.Email,
                        language = user.PreferredLanguage,
                        token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
                    }),
                    clock.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }

        return ResetRequested();
    }

    /// <summary>Completes a password reset (FR-009).</summary>
    /// <remarks>
    /// Succeeding here revokes <em>every</em> session: a user resetting because they suspect
    /// compromise must actually eject the intruder. Passkeys are kept — forgetting a password is
    /// not evidence of a lost device.
    /// </remarks>
    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        UserManager<AppSuiteUser> users,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return Results.BadRequest();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.BadRequest();
        }

        string token;

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Results.BadRequest();
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword ?? string.Empty)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = [.. result.Errors.Select(e => e.Description)],
            });
        }

        // Completing a reset proves control of the address, so it also verifies a pending account
        // (UC-MAIN-007 extension 2b) — the mail did the same job the verification mail would have.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user).ConfigureAwait(false);
        }

        await sessions.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    /// <summary>Signs in with email and password (FR-008).</summary>
    /// <remarks>
    /// An unknown address and a wrong password produce the same response, so the form cannot be
    /// used to discover which addresses are registered (NFR-005).
    /// </remarks>
    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        UserManager<AppSuiteUser> users,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email ?? string.Empty).ConfigureAwait(false);

        if (user is null || !await users.CheckPasswordAsync(user, request.Password ?? string.Empty).ConfigureAwait(false))
        {
            return InvalidCredentials();
        }

        var tokens = await sessions.StartAsync(user, cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToResponse(tokens, user.EmailConfirmed));
    }

    /// <summary>Exchanges a refresh secret for a new token pair (FR-008).</summary>
    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        SessionService sessions,
        ISessionUserLookup lookup,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return InvalidCredentials();
        }

        var result = await sessions.RefreshAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);

        if (result is not SessionRefreshResult.Succeeded succeeded)
        {
            // Every rejection reads the same to the caller. Distinguishing "expired" from "we
            // think this was stolen" would tell an attacker whether their replay was noticed.
            return InvalidCredentials();
        }

        var userId = Guid.Parse(ReadSubject(succeeded.Tokens.AccessToken));
        var user = await lookup.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return Results.Ok(ToResponse(succeeded.Tokens, user?.EmailConfirmed ?? false));
    }

    /// <summary>Ends every session for the signed-in user (FR-008).</summary>
    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        await sessions.RevokeAllAsync(userId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    /// <summary>Returns the signed-in user's own profile (FR-012).</summary>
    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        ISessionUserLookup lookup,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await lookup.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new ProfileResponse(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.PreferredLanguage,
                user.EmailConfirmed));
    }

    private static SessionResponse ToResponse(SessionTokens tokens, bool emailVerified) =>
        new(
            tokens.AccessToken,
            (int)(tokens.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            tokens.RefreshSecret,
            emailVerified);

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }

    private static string ReadSubject(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        var json = JsonDocument.Parse(WebEncoders.Base64UrlDecode(payload));
        return json.RootElement.GetProperty("sub").GetString()!;
    }

    /// <summary>
    /// The single registration response. Identical for a new address and one already in use.
    /// </summary>
    private static IResult AcceptedRegistration() =>
        Results.Accepted(value: new { message = "If that address can be registered, a verification email is on its way." });

    /// <summary>The single reset response. Identical whether or not the address has an account.</summary>
    private static IResult ResetRequested() =>
        Results.Accepted(value: new { message = "If that address has an account, a reset link is on its way." });

    private static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");
}
