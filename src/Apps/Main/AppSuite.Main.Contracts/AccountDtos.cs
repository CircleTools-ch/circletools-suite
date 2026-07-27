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
