namespace CircleTools.Main.Contracts;

/// <summary>Sign-up request (FR-007).</summary>
/// <param name="DisplayName">How the person appears to others sharing a circle.</param>
/// <param name="Email">The account's address and recovery channel.</param>
/// <param name="Password">Minimum 12 characters; no composition rules.</param>
/// <param name="Language">Preferred language (<c>de-CH</c>, <c>en</c>, <c>fr-CH</c>).</param>
/// <param name="SourceApp">
/// The app the sign-up came from (ADR-0064), so the verification mail is branded — and links back — to
/// that app. Stamped by the client; <see langword="null"/> falls back to the default branding.
/// </param>
public sealed record RegisterRequest(string DisplayName, string Email, string Password, string? Language, string? SourceApp = null);

/// <summary>Sign-in request (FR-008).</summary>
/// <param name="Email">The account's address.</param>
/// <param name="Password">The account's password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Exchange of a refresh secret for a new token pair (FR-008).</summary>
/// <param name="RefreshToken">The refresh secret previously issued.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Sign-out of this device's session (FR-008, ADR-0079).</summary>
/// <param name="RefreshToken">
/// The refresh secret this device holds — it names the session to end. Without one (an app built before
/// ADR-0079) the server ends every session, as it used to.
/// </param>
public sealed record LogoutRequest(string? RefreshToken);

/// <summary>Asks for the signed-in devices (ADR-0079 amendment).</summary>
/// <param name="RefreshToken">This device's refresh secret, so its row is marked; optional.</param>
public sealed record SessionListRequest(string? RefreshToken);

/// <summary>One signed-in device (ADR-0079 amendment).</summary>
/// <param name="Id">The session id — what "sign out this device" names.</param>
/// <param name="Device">A coarse label ("Chrome · Windows"); empty when unknown.</param>
/// <param name="SignedInAt">When it signed in.</param>
/// <param name="LastActiveAt">When it was last active.</param>
/// <param name="IsCurrent">Whether it is the device asking.</param>
public sealed record SessionInfoResponse(Guid Id, string Device, DateTimeOffset SignedInAt, DateTimeOffset LastActiveAt, bool IsCurrent);

/// <summary>Change of the password by a signed-in user (FR-049, ADR-0079).</summary>
/// <param name="CurrentPassword">The password the account has now.</param>
/// <param name="NewPassword">The replacement password; same policy as sign-up.</param>
/// <param name="RefreshToken">The refresh secret of this device's session, which stays signed in; every other session ends.</param>
/// <param name="SourceApp">The app the change came from, for the notice mail's branding (ADR-0064).</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string? RefreshToken, string? SourceApp = null);

/// <summary>
/// Request to mint a cross-app single-sign-on authorization code (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// Sent by the <em>authority</em> app while it holds a live session, on behalf of a sibling app that
/// wants to start its own session. Bearer-authorized: the code is minted for the caller's user.
/// </remarks>
/// <param name="Client">An identifier for the requesting app (for auditing/binding).</param>
/// <param name="ReturnUri">Where the code is redeemed — its origin must be on the suite allow-list.</param>
/// <param name="CodeChallenge">The PKCE challenge; the redeemer proves the matching verifier.</param>
/// <param name="CodeChallengeMethod">The PKCE method; only <c>S256</c> is supported.</param>
public sealed record ConnectAuthorizeRequest(
    string Client,
    string ReturnUri,
    string CodeChallenge,
    string CodeChallengeMethod);

/// <summary>The minted single-use authorization code (FR-031, ADR-0039).</summary>
/// <param name="Code">The short-lived code to carry back to the requesting app.</param>
public sealed record ConnectAuthorizeResponse(string Code);

/// <summary>
/// Request to redeem a single-sign-on authorization code for a session (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// Sent by the <em>requesting</em> app through the anonymous client. On success it returns the same
/// <see cref="SessionResponse"/> a password sign-in returns — the client stores it identically.
/// </remarks>
/// <param name="Code">The code received from the authority redirect.</param>
/// <param name="CodeVerifier">The PKCE verifier proving this app requested the code.</param>
public sealed record ConnectTokenRequest(string Code, string CodeVerifier);

/// <summary>Request for a password-reset link (FR-009).</summary>
/// <param name="Email">The address to send the link to, if it has an account.</param>
/// <param name="SourceApp">The app the request came from, for mail branding + link origin (ADR-0064).</param>
public sealed record ForgotPasswordRequest(string Email, string? SourceApp = null);

/// <summary>Request for a replacement verification link (FR-007).</summary>
/// <param name="Email">The address to re-send to, if it has an unverified account.</param>
/// <param name="SourceApp">The app the request came from, for mail branding + link origin (ADR-0064).</param>
public sealed record ResendVerificationRequest(string Email, string? SourceApp = null);

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
/// <param name="SourceApp">The app the request came from, for mail branding + link origin (ADR-0064).</param>
public sealed record ChangeEmailRequest(string NewEmail, string? SourceApp = null);

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
/// <param name="DeleteSharedGroups">
/// The owner's disposition for the online groups they own (FR-043, ADR-0086): <see langword="false"/> (default)
/// hands them over to an organizer link so the people taking part keep access; <see langword="true"/> deletes
/// those online copies too.
/// </param>
/// <param name="SourceApp">The app the request came from, for the farewell mail's branding (ADR-0064, FR-047).</param>
public sealed record DeleteAccountRequest(string Password, bool DeleteSharedGroups = false, string? SourceApp = null);

/// <summary>
/// How many online groups the account owns, so the delete screen can offer their disposition (FR-043,
/// <c>GET /api/v1/account/owned-groups</c>).
/// </summary>
/// <param name="Count">The number of groups.</param>
public sealed record OwnedGroupCount(int Count);

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
