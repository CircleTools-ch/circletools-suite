// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CircleTools.Identity;
using CircleTools.Identity.Passwords;
using CircleTools.Identity.Tokens;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;

namespace CircleTools.Main.Service;

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

    /// <summary>Outbox message type for the confirmation sent to a new address.</summary>
    public const string EmailChangeMailType = "mail.email-change";

    /// <summary>Outbox message type for the notice sent to the address being replaced.</summary>
    public const string EmailChangeNoticeMailType = "mail.email-change-notice";

    /// <summary>Outbox message type for the notice that the password was changed (FR-049, ADR-0079).</summary>
    public const string PasswordChangedMailType = "mail.password-changed";

    /// <summary>Outbox message type for the farewell confirmation sent after deletion (FR-047, ADR-0064).</summary>
    public const string AccountDeletedMailType = "mail.account-deleted";

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
        group.MapPost("/resend-verification", ResendVerificationAsync).WithName("ResendVerification").RequireConstantTime().AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/forgot-password", ForgotPasswordAsync).WithName("ForgotPassword").RequireConstantTime().AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/reset-password", ResetPasswordAsync).WithName("ResetPassword").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/refresh", RefreshAsync).WithName("Refresh").AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).WithName("Logout").RequireAuthorization();
        group.MapPost("/logout-all", LogoutAllAsync).WithName("LogoutAll").RequireAuthorization();
        group.MapPost("/sessions", ListSessionsAsync).WithName("ListSessions").RequireAuthorization();
        group.MapPost("/sessions/{sessionId:guid}/revoke", RevokeSessionAsync).WithName("RevokeSession").RequireAuthorization();
        group.MapPost("/change-password", ChangePasswordAsync).WithName("ChangePassword")
            .RequireAuthorization().RequireRateLimiting(SensitiveRateLimitPolicy);

        // Cross-app SSO hand-off (FR-031, ADR-0039). Authorize is bearer-authorized (the authority
        // mints a code for its signed-in user); token is anonymous (the sibling redeems it), for the
        // same reason /refresh is anonymous — it must not pass through the authorizing handler.
        group.MapPost("/connect/authorize", ConnectAuthorizeAsync).WithName("ConnectAuthorize").RequireAuthorization();
        group.MapPost("/connect/token", ConnectTokenAsync).WithName("ConnectToken").AllowAnonymous();
        group.MapGet("/profile", GetProfileAsync).WithName("GetProfile").RequireAuthorization();
        group.MapPut("/profile", UpdateProfileAsync).WithName("UpdateProfile").RequireAuthorization();

        group.MapPost("/export", ExportAsync).WithName("ExportData").RequireAuthorization();
        group.MapPost("/delete", DeleteAccountAsync).WithName("DeleteAccount")
            .RequireAuthorization().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapGet("/owned-groups", GetOwnedGroupCountAsync).WithName("GetOwnedGroupCount").RequireAuthorization();

        group.MapPost("/change-email", ChangeEmailAsync).WithName("ChangeEmail")
            .RequireConstantTime().RequireAuthorization().RequireRateLimiting(SensitiveRateLimitPolicy);
        group.MapPost("/confirm-email-change", ConfirmEmailChangeAsync).WithName("ConfirmEmailChange")
            .RequireAuthorization().RequireRateLimiting(SensitiveRateLimitPolicy);

        return group;
    }

    /// <summary>
    /// Creates a pending account and queues its verification mail (FR-007).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The response is identical whether or not the address already has an account. An address in
    /// use instead receives a "someone tried to sign up" mail, so the real owner learns of the
    /// attempt while the requester learns nothing (NFR-005). Both paths queue exactly one message.
    /// </para>
    /// <para>
    /// <strong>Queueing one message each is not by itself enough to make them take the same time</strong>,
    /// which is what this comment used to claim. Creating an account hashes the password, and hashing
    /// is deliberately slow, so the free-address path measured <strong>18x slower</strong> than the
    /// taken-address path — a fast answer meant "already registered". The taken-address path now
    /// spends the same hash through <see cref="DecoyPasswordVerifier"/>.
    /// </para>
    /// </remarks>
    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        UserManager<CircleToolsUser> users,
        IOutbox outbox,
        TimeProvider clock,
        DecoyPasswordVerifier decoy,
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
            // Pay for the password hash the other branch pays for, or the two are trivially
            // distinguishable by time (NFR-005).
            decoy.Spend(request.Password);

            await outbox.EnqueueAsync(
                OutboxMessage.Create(
                    SignUpAttemptMailType,
                    JsonSerializer.Serialize(new { email = request.Email, language = existing.PreferredLanguage, sourceApp = request.SourceApp }),
                    now),
                cancellationToken).ConfigureAwait(false);

            return AcceptedRegistration();
        }

        var user = new CircleToolsUser
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
            return PasswordRejected(created);
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
                    sourceApp = request.SourceApp,
                }),
                now),
            cancellationToken).ConfigureAwait(false);

        return AcceptedRegistration();
    }

    /// <summary>Confirms an address and marks the account verified (FR-007).</summary>
    private static async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        UserManager<CircleToolsUser> users)
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

    /// <summary>
    /// Queues a fresh verification link for an address that has not been confirmed yet (FR-007).
    /// </summary>
    /// <remarks>
    /// Verification links are the one step of sign-up the user cannot retry on their own — the mail
    /// can be lost, filtered, or simply deleted, and without this the account is stranded.
    /// <para>
    /// The response is the same 202 as <c>/register</c>, in every case: unknown address, already
    /// verified, or genuinely resent. Answering differently would turn this into the enumeration
    /// oracle that sign-up itself carefully is not (NFR-005), and "already verified" is exactly the
    /// fact an attacker wants.
    /// </para>
    /// <para>
    /// Only the resent case generates a token and queues a mail — so the two cases an attacker is
    /// asking about are also the two cheap ones, and the gap measured 1.67x. <c>RequireConstantTime</c>
    /// closes it; without the filter the branch structure here reads the question aloud.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ResendVerificationAsync(
        [FromBody] ResendVerificationRequest request,
        UserManager<CircleToolsUser> users,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await users.FindByEmailAsync(request.Email).ConfigureAwait(false);

        if (user is not null && !user.EmailConfirmed)
        {
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
                        sourceApp = request.SourceApp,
                    }),
                    clock.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }

        return AcceptedRegistration();
    }

    /// <summary>Queues a password-reset link (FR-009).</summary>
    /// <remarks>
    /// <para>
    /// The response is identical for a registered and an unregistered address, and no mail is sent
    /// for the latter. Requesting a reset does <em>not</em> invalidate the current password — a
    /// stranger entering someone's address must not be able to lock them out (NFR-005).
    /// </para>
    /// <para>
    /// <strong>Identical is not indistinguishable.</strong> A known address pays for a reset token
    /// and an outbox row that an unknown one skips entirely, and that showed up as a steady 1.9x
    /// difference — 2.36 ms against 1.24 ms — for the whole of M1, under a response body that was
    /// carefully identical the entire time. The route now carries <c>RequireConstantTime</c>, which
    /// is what makes the sentence above true rather than merely intended.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        UserManager<CircleToolsUser> users,
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
                        sourceApp = request.SourceApp,
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
        UserManager<CircleToolsUser> users,
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
            return PasswordRejected(result);
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
        UserManager<CircleToolsUser> users,
        SessionService sessions,
        DecoyPasswordVerifier decoy,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email ?? string.Empty).ConfigureAwait(false);

        if (user is null)
        {
            // No account, so nothing to verify - but returning now would answer in a few
            // milliseconds where a real account takes tens of them, and that difference alone
            // reveals which addresses are registered (NFR-005). Measured at 20x before this call
            // existed. The short-circuit `user is null || !await CheckPassword...` that used to
            // stand here reads as equivalent and is not.
            decoy.Spend(request.Password);

            return InvalidCredentials();
        }

        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty).ConfigureAwait(false))
        {
            return InvalidCredentials();
        }

        var tokens = await sessions.StartAsync(user, DeviceOf(http), cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Mints a single-use, PKCE-bound SSO authorization code for the signed-in user (FR-031, ADR-0039).
    /// </summary>
    /// <remarks>
    /// Bearer-authorized: the code is minted for the caller's own session, on behalf of a sibling app
    /// named by <c>Client</c> that will redeem it. The <c>ReturnUri</c> is validated against the suite
    /// allow-list (the same origins CORS trusts) before any code exists — an untrusted origin gets a
    /// bare 400 and learns nothing.
    /// </remarks>
    private static async Task<IResult> ConnectAuthorizeAsync(
        [FromBody] ConnectAuthorizeRequest request,
        ClaimsPrincipal principal,
        ConnectAuthorizationCodeService codes,
        ConnectReturnUriValidator returnUris,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (!string.Equals(request.CodeChallengeMethod, Pkce.MethodS256, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.CodeChallenge))
        {
            return Results.BadRequest();
        }

        if (!returnUris.IsAllowed(request.ReturnUri))
        {
            return Results.BadRequest();
        }

        var code = await codes.IssueAsync(
            userId,
            request.Client,
            request.ReturnUri,
            request.CodeChallenge,
            request.CodeChallengeMethod,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(new ConnectAuthorizeResponse(code));
    }

    /// <summary>
    /// Redeems an SSO authorization code for a session (FR-031, ADR-0039).
    /// </summary>
    /// <remarks>
    /// Anonymous, PKCE-proven, single-use. The code yields only a user id; the session itself comes
    /// from <see cref="SessionService.StartAsync(CircleToolsUser, string?, CancellationToken)"/>, so this is the same token contract as a password
    /// sign-in and there is exactly one path that issues tokens. Every rejection reads identically, as
    /// a bad refresh does.
    /// </remarks>
    private static async Task<IResult> ConnectTokenAsync(
        [FromBody] ConnectTokenRequest request,
        ConnectAuthorizationCodeService codes,
        SessionService sessions,
        UserManager<CircleToolsUser> users,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = await codes.RedeemAsync(request.Code, request.CodeVerifier, cancellationToken).ConfigureAwait(false);

        if (userId is null)
        {
            return InvalidCredentials();
        }

        var user = await users.FindByIdAsync(userId.Value.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var tokens = await sessions.StartAsync(user, DeviceOf(http), cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToResponse(tokens, user.EmailConfirmed));
    }

    /// <summary>Ends this device's session (FR-008, ADR-0079).</summary>
    /// <remarks>
    /// The session is named by the refresh secret in the body. A request without one comes from an app built
    /// before ADR-0079, which promised its user that signing out ends every session — so it still does.
    /// </remarks>
    private static async Task<IResult> LogoutAsync(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request,
        ClaimsPrincipal principal,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            await sessions.RevokeAllAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sessions.RevokeSessionAsync(userId, request.RefreshToken, cancellationToken).ConfigureAwait(false);
        }

        return Results.NoContent();
    }

    /// <summary>The signed-in devices, this one marked (ADR-0079 amendment).</summary>
    private static async Task<IResult> ListSessionsAsync(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SessionListRequest? request,
        ClaimsPrincipal principal,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var list = await sessions.ListAsync(userId, request?.RefreshToken, cancellationToken).ConfigureAwait(false);
        return Results.Ok(list.Select(s => new SessionInfoResponse(s.Id, s.Device, s.SignedInAt, s.LastActiveAt, s.IsCurrent)).ToList());
    }

    /// <summary>Signs out one device of the signed-in user (ADR-0079 amendment). Someone else's session: 404.</summary>
    private static async Task<IResult> RevokeSessionAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return await sessions.RevokeSessionByIdAsync(userId, sessionId, cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    // The coarse device label for a new session, from the request's User-Agent (ADR-0079 amendment).
    internal static string DeviceOf(HttpContext http) => DeviceLabel.FromUserAgent(http.Request.Headers.UserAgent.ToString());

    /// <summary>Ends every session of the signed-in user, on every device (FR-049, ADR-0079).</summary>
    private static async Task<IResult> LogoutAllAsync(
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

    /// <summary>Changes the signed-in user's password and ends their other sessions (FR-049, ADR-0079).</summary>
    /// <remarks>
    /// A wrong current password gets exactly the failed sign-in answer (NFR-005), and the endpoint carries
    /// the sign-in rate limit. The new password goes through the same validators as sign-up (policy and the
    /// breached-password check). The session that made the change stays; every other one ends, and a
    /// link-less notice goes to the account's address.
    /// </remarks>
    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        SessionService sessions,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null || !await users.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty).ConfigureAwait(false))
        {
            return InvalidCredentials();
        }

        var result = await users
            .ChangePasswordAsync(user, request.CurrentPassword ?? string.Empty, request.NewPassword ?? string.Empty)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return PasswordRejected(result);
        }

        await sessions.RevokeOtherSessionsAsync(user.Id, request.RefreshToken, cancellationToken).ConfigureAwait(false);

        await outbox.EnqueueAsync(
            OutboxMessage.Create(
                PasswordChangedMailType,
                JsonSerializer.Serialize(new { email = user.Email, language = user.PreferredLanguage, sourceApp = request.SourceApp }),
                clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

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

    /// <summary>Updates display name and preferred language (FR-012).</summary>
    private static async Task<IResult> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                // A user must stay identifiable to the people they share circles with.
                ["displayName"] = ["A display name is required."],
            });
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Deliberately no uniqueness check: two "Anna"s in one family is ordinary (FR-012).
        user.DisplayName = request.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            user.PreferredLanguage = request.Language;
        }

        var updated = await users.UpdateAsync(user).ConfigureAwait(false);

        if (!updated.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["profile"] = [.. updated.Errors.Select(e => e.Description)],
            });
        }

        return Results.Ok(new ProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.PreferredLanguage,
            user.EmailConfirmed));
    }

    /// <summary>Starts a verify-before-swap email change (FR-013).</summary>
    /// <remarks>
    /// The account keeps its current address until the new one is confirmed, so a typo or a
    /// hijacked session cannot silently move the recovery channel out of the owner's reach. The
    /// old address is told a change was requested while it still controls the account.
    /// <para>
    /// If the new address already belongs to someone, the response is unchanged and no mail is
    /// sent (NFR-005). That costs the user an explanation — the same "flying blind" cost
    /// [ADR-0018] accepts for invitations — and is preferred to confirming which addresses are
    /// registered.
    /// </para>
    /// <para>
    /// The asymmetry here is the largest of the four and runs the other way: a free address costs a
    /// token and <em>two</em> outbox rows, a taken one costs nothing, so the fast answer meant
    /// "taken" — measured at 1.63x. Mirroring that work would mean writing rows nobody reads, so the
    /// route carries <c>RequireConstantTime</c> instead.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ChangeEmailAsync(
        [FromBody] ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.NewEmail))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newEmail"] = ["An email address is required."],
            });
        }

        var taken = await users.FindByEmailAsync(request.NewEmail).ConfigureAwait(false);

        if (taken is null)
        {
            var token = await users.GenerateChangeEmailTokenAsync(user, request.NewEmail).ConfigureAwait(false);
            var now = clock.GetUtcNow();

            await outbox.EnqueueAsync(
                OutboxMessage.Create(
                    EmailChangeMailType,
                    JsonSerializer.Serialize(new
                    {
                        userId = user.Id,
                        email = request.NewEmail,
                        language = user.PreferredLanguage,
                        token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
                        sourceApp = request.SourceApp,
                    }),
                    now),
                cancellationToken).ConfigureAwait(false);

            await outbox.EnqueueAsync(
                OutboxMessage.Create(
                    EmailChangeNoticeMailType,
                    JsonSerializer.Serialize(new { email = user.Email, language = user.PreferredLanguage, sourceApp = request.SourceApp }),
                    now),
                cancellationToken).ConfigureAwait(false);
        }

        return EmailChangeRequested();
    }

    /// <summary>Completes an email change once the new address is proven (FR-013).</summary>
    private static async Task<IResult> ConfirmEmailChangeAsync(
        [FromBody] ConfirmEmailChangeRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users)
    {
        if (!TryGetUserId(principal, out var callerId)
            || !Guid.TryParse(request.UserId, out var userId)
            || callerId != userId)
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
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

        var result = await users.ChangeEmailAsync(user, request.NewEmail, token).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Results.BadRequest();
        }

        // UserName mirrors Email in this suite (sign-in is by address), so it has to move too —
        // otherwise the old address would still resolve the account at sign-in.
        await users.SetUserNameAsync(user, request.NewEmail).ConfigureAwait(false);

        return Results.NoContent();
    }

    /// <summary>Produces a machine-readable copy of the caller's data (FR-014).</summary>
    /// <remarks>
    /// The archive mirrors in-app visibility, and in M1 that is genuinely just the account itself:
    /// circles (M2), synced groups (M3) and assignments (M4) do not exist server-side yet. The
    /// <c>Notes</c> field says so, so an archive that looks empty is not mistaken for data loss.
    /// <para>
    /// Delivered inline because an M1 export is a few hundred bytes. The short-lived single-use
    /// link [ADR-0022] requires is only needed once there is enough content to generate
    /// asynchronously.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ExportAsync(
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        IRefreshTokenChainStore chains,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var passkeys = await users.GetPasskeysAsync(user).ConfigureAwait(false);
        var sessions = await chains.CountActiveForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var export = new DataExport(
            clock.GetUtcNow(),
            new ProfileResponse(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.PreferredLanguage,
                user.EmailConfirmed),
            // Base64Url, matching PasskeyResponse and the DELETE /passkeys/{credentialId} route.
            // Plain Base64 here would produce ids from the export that the delete endpoint rejects.
            [.. passkeys.Select(p => new ExportedPasskey(WebEncoders.Base64UrlEncode(p.CredentialId), p.CreatedAt))],
            sessions,
            [
                // Circles exist as of M2, so the old "circles are not part of this release" note
                // would now be false. What the archive still omits is their *content* — that is
                // FR-014's next slice, and saying so is better than a thin archive being read as
                // data loss (ADR-0022).
                "Your Circles are not in this archive yet; you can see them in the app.",
                "Secret-Santa data is stored on your own device and is not synced to the server yet.",
            ]);

        return Results.Ok(export);
    }

    /// <summary>Deletes the account immediately and irreversibly (FR-015, ADR-0022).</summary>
    /// <remarks>
    /// <para>
    /// Re-authenticates first: deletion cannot be undone, so a session left open on a shared
    /// device must not be enough. Local data on the user's own devices is untouched — that is what
    /// makes immediate deletion defensible ([ADR-0017]).
    /// </para>
    /// <para>
    /// The order of the four steps is the whole design. Guards run before anything changes, because
    /// ADR-0022 leaves nothing to roll back. Then sessions, then the other contexts' cleanups, and
    /// the account row last — so that every partial failure leaves the recoverable state rather than
    /// the corrupt one. See <see cref="IAccountDeletionCleanup"/> for why cleanup precedes the
    /// delete and not the other way round.
    /// </para>
    /// </remarks>
    // FR-043: the delete screen offers a disposition for the online groups the account owns.
    private static async Task<IResult> GetOwnedGroupCountAsync(
        ClaimsPrincipal principal,
        CircleTools.SecretSanta.Service.Core.Groups.GroupService onlineGroups,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new OwnedGroupCount(await onlineGroups.CountOwnedAsync(userId, cancellationToken).ConfigureAwait(false)));
    }

    private static async Task<IResult> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        ClaimsPrincipal principal,
        UserManager<CircleToolsUser> users,
        SessionService sessions,
        IEnumerable<IAccountDeletionGuard> guards,
        IEnumerable<IAccountDeletionCleanup> cleanups,
        CircleTools.SecretSanta.Service.Core.Groups.GroupService onlineGroups,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty).ConfigureAwait(false))
        {
            return InvalidCredentials();
        }

        // Captured before the row goes, so the farewell mail can be queued once deletion has succeeded.
        var farewellEmail = user.Email;
        var farewellLanguage = user.PreferredLanguage;

        foreach (var guard in guards)
        {
            var refusal = await guard.CheckAsync(user.Id, cancellationToken).ConfigureAwait(false);

            if (refusal is not null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: refusal.Title,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = refusal.Code,
                        ["blockers"] = refusal.Blockers,
                    });
            }
        }

        // Sessions first: if the delete fails afterwards the account is at worst signed out, which
        // is recoverable. The reverse order could leave live tokens for a deleted account.
        await sessions.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // The owner's online-group disposition (FR-043): by default the group store's cleanup below hands
        // owned groups over to an organizer link so participants keep access. If the user chose to delete
        // them too, remove them first — the cleanup then finds nothing left to hand over.
        if (request.DeleteSharedGroups)
        {
            await onlineGroups.CleanUpAccountAsync(user.Id, deleteOwnedGroups: true, cancellationToken).ConfigureAwait(false);
        }

        // Then the other contexts, while the account still exists to be cleaned up after.
        foreach (var cleanup in cleanups)
        {
            await cleanup.CleanUpAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }

        var deleted = await users.DeleteAsync(user).ConfigureAwait(false);

        if (!deleted.Succeeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Deletion failed.");
        }

        // Farewell confirmation (FR-047, ADR-0064): queued only after the account is really gone, so it
        // confirms what happened rather than pre-announcing it. The payload is self-contained, so the
        // deleted row is irrelevant to rendering it; it carries no link and no assignment.
        await outbox.EnqueueAsync(
            OutboxMessage.Create(
                AccountDeletedMailType,
                JsonSerializer.Serialize(new { email = farewellEmail, language = farewellLanguage, sourceApp = request.SourceApp }),
                clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
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
    /// Reports a password-policy rejection as machine-readable <em>codes</em> (FR-007, NFR-003).
    /// </summary>
    /// <remarks>
    /// Identity's <c>Description</c> is English prose, and the product is DE/EN/FR (ADR-0011); a
    /// client cannot translate a sentence, but it can translate <c>PasswordTooShort</c> or
    /// <c>BreachedPassword</c>. This is the change ADR-0027 triggers by taking the policy past a
    /// single rule. Codes leak nothing about other accounts — they describe the caller's own input.
    /// </remarks>
    private static IResult PasswordRejected(IdentityResult result) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["password"] = [.. result.Errors.Select(e => e.Code)],
        });

    /// <summary>
    /// The single registration response. Identical for a new address and one already in use.
    /// </summary>
    private static IResult AcceptedRegistration() =>
        Results.Accepted(value: new { message = "If that address can be registered, a verification email is on its way." });

    /// <summary>The single reset response. Identical whether or not the address has an account.</summary>
    private static IResult ResetRequested() =>
        Results.Accepted(value: new { message = "If that address has an account, a reset link is on its way." });

    /// <summary>The single email-change response. Identical whether or not the address is taken.</summary>
    private static IResult EmailChangeRequested() =>
        Results.Accepted(value: new { message = "If that address can be used, a confirmation email is on its way." });

    private static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");
}
