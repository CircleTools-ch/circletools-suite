namespace AppSuite.Main.Contracts;

/// <summary>Sign-up request (FR-007).</summary>
/// <param name="DisplayName">How the person appears to others sharing a circle.</param>
/// <param name="Email">The account's address and recovery channel.</param>
/// <param name="Password">Minimum 12 characters; no composition rules.</param>
/// <param name="Language">Preferred language (<c>de-CH</c>, <c>en</c>, <c>fr-CH</c>).</param>
public sealed record RegisterRequest(string DisplayName, string Email, string Password, string? Language);

/// <summary>Sign-in request (FR-008).</summary>
/// <param name="Email">The account's address.</param>
/// <param name="Password">The account's password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Exchange of a refresh secret for a new token pair (FR-008).</summary>
/// <param name="RefreshToken">The refresh secret previously issued.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Request for a password-reset link (FR-009).</summary>
/// <param name="Email">The address to send the link to, if it has an account.</param>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Completion of a password reset (FR-009).</summary>
/// <param name="UserId">The account being reset.</param>
/// <param name="Token">The single-use token from the reset link.</param>
/// <param name="NewPassword">The replacement password; same policy as sign-up.</param>
public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);

/// <summary>Profile update (FR-012).</summary>
/// <param name="DisplayName">The name others sharing a circle see. Required, not unique.</param>
/// <param name="Language">Preferred language (<c>de-CH</c>, <c>en</c>, <c>fr-CH</c>).</param>
public sealed record UpdateProfileRequest(string DisplayName, string Language);

/// <summary>Request to move the account to a new address (FR-013).</summary>
/// <param name="NewEmail">The address to move to, once verified.</param>
public sealed record ChangeEmailRequest(string NewEmail);

/// <summary>Completion of an email change (FR-013).</summary>
/// <param name="UserId">The account changing address.</param>
/// <param name="NewEmail">The address being confirmed.</param>
/// <param name="Token">The token from the link sent to the new address.</param>
public sealed record ConfirmEmailChangeRequest(string UserId, string NewEmail, string Token);

/// <summary>Account-deletion request (FR-015).</summary>
/// <param name="Password">
/// The current password. Deletion is immediate and irreversible, so it re-authenticates rather
/// than trusting a session that may have been left open.
/// </param>
public sealed record DeleteAccountRequest(string Password);

/// <summary>
/// The exported copy of a user's data (FR-014). Mirrors what the user can already see in the app.
/// </summary>
/// <param name="ExportedAt">When the archive was produced.</param>
/// <param name="Profile">The account's own profile.</param>
/// <param name="Passkeys">Registered passkeys, by label and creation date — never key material.</param>
/// <param name="Sessions">Active session count; sessions themselves are not personal content.</param>
/// <param name="Notes">
/// What is deliberately absent, so an empty-looking archive is not mistaken for data loss.
/// </param>
public sealed record DataExport(
    DateTimeOffset ExportedAt,
    ProfileResponse Profile,
    IReadOnlyList<ExportedPasskey> Passkeys,
    int Sessions,
    IReadOnlyList<string> Notes);

/// <summary>A passkey as it appears in an export (FR-014).</summary>
/// <param name="CredentialId">The credential's public identifier.</param>
/// <param name="CreatedAt">When it was registered, if known.</param>
public sealed record ExportedPasskey(string CredentialId, DateTimeOffset? CreatedAt);

/// <summary>Registration of a newly created passkey (FR-010).</summary>
/// <param name="CredentialJson">JSON-serialized result of <c>navigator.credentials.create()</c>.</param>
/// <param name="Label">A name the user recognises the device by; defaulted when omitted.</param>
public sealed record RegisterPasskeyRequest(string CredentialJson, string? Label);

/// <summary>Passkey sign-in (FR-011).</summary>
/// <param name="CredentialJson">JSON-serialized result of <c>navigator.credentials.get()</c>.</param>
public sealed record PasskeyLoginRequest(string CredentialJson);

/// <summary>A registered passkey as shown in security settings (FR-010).</summary>
/// <param name="CredentialId">Base64Url public identifier of the credential.</param>
/// <param name="Label">The user-chosen name.</param>
/// <param name="CreatedAt">When it was registered.</param>
public sealed record PasskeyResponse(string CredentialId, string? Label, DateTimeOffset CreatedAt);

/// <summary>Email-verification request (FR-007).</summary>
/// <param name="UserId">The account being verified.</param>
/// <param name="Token">The token from the verification link.</param>
public sealed record VerifyEmailRequest(string UserId, string Token);

/// <summary>A signed-in session.</summary>
/// <param name="AccessToken">Short-lived; the client holds this in memory only (ADR-0021).</param>
/// <param name="ExpiresIn">Access-token lifetime in seconds.</param>
/// <param name="RefreshToken">Rotated on every use.</param>
/// <param name="EmailVerified">
/// False for a pending account: it may sign in, but nothing account-scoped works yet (FR-007).
/// </param>
public sealed record SessionResponse(string AccessToken, int ExpiresIn, string RefreshToken, bool EmailVerified);

/// <summary>The signed-in user's own profile (FR-012).</summary>
/// <param name="Id">The user's identity.</param>
/// <param name="DisplayName">The name others see.</param>
/// <param name="Email">The account address — never exposed to other members.</param>
/// <param name="Language">Preferred language.</param>
/// <param name="EmailVerified">Whether the address has been proven.</param>
public sealed record ProfileResponse(Guid Id, string DisplayName, string Email, string Language, bool EmailVerified);
