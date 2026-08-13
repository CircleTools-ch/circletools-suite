using System.Net.Http.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Ui.Account.Api;

/// <summary>
/// The typed client over <c>/api/v1/account</c> (FEAT-002).
/// </summary>
/// <remarks>
/// Every method returns an <see cref="ApiResult"/>: expected failures are values here, not
/// exceptions, so a page renders a message instead of catching one. The only exception that escapes
/// is a genuine transport failure, and even that is caught and reported as
/// <see cref="ApiFailure.Unreachable"/> — being offline is an ordinary state for this app, not a
/// crash.
/// </remarks>
/// <param name="authorized">The client that carries the bearer token.</param>
/// <param name="anonymous">The client used before there is a session.</param>
public sealed class AccountApiClient(
    [FromKeyedServices(AccountApiClients.Authorized)] HttpClient authorized,
    [FromKeyedServices(AccountApiClients.Anonymous)] HttpClient anonymous)
{
    private const string Route = AccountApiClients.AccountRoute;

    /// <summary>Signs up and triggers the verification mail (FR-007).</summary>
    /// <param name="request">The sign-up details.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome; success carries no body worth showing.</returns>
    public Task<ApiResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        PostAsync(anonymous, $"{Route}/register", request, cancellationToken);

    /// <summary>Confirms an address from a mail link (FR-007).</summary>
    /// <param name="request">The user id and token from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default) =>
        PostAsync(anonymous, $"{Route}/verify-email", request, cancellationToken);

    /// <summary>Asks for a replacement verification link (FR-007).</summary>
    /// <param name="request">The address to re-send to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome, which is the same whatever the address turns out to be.</returns>
    public Task<ApiResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(anonymous, $"{Route}/resend-verification", request, cancellationToken);

    /// <summary>Requests a password-reset link (FR-009).</summary>
    /// <param name="request">The address to send to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome, which is the same whether or not the address has an account.</returns>
    public Task<ApiResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(anonymous, $"{Route}/forgot-password", request, cancellationToken);

    /// <summary>Completes a password reset (FR-009).</summary>
    /// <param name="request">The user id, token and new password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(anonymous, $"{Route}/reset-password", request, cancellationToken);

    /// <summary>Signs in with email and password (FR-008).</summary>
    /// <param name="request">The credentials.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new session, or the failure.</returns>
    public Task<ApiResult<SessionResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<LoginRequest, SessionResponse>(anonymous, $"{Route}/login", request, cancellationToken);

    /// <summary>Ends every session for the signed-in user (FR-008).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default) =>
        PostAsync<object?>(authorized, $"{Route}/logout", null, cancellationToken);

    /// <summary>
    /// As the SSO authority, mints a single-use authorization code for a sibling app (FR-031, ADR-0039).
    /// </summary>
    /// <remarks>Uses the <em>authorized</em> client: the code is minted for this app's own session.</remarks>
    /// <param name="request">The client id, return URI and PKCE challenge.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The code to carry back to the requesting app, or the failure.</returns>
    public Task<ApiResult<ConnectAuthorizeResponse>> ConnectAuthorizeAsync(
        ConnectAuthorizeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ConnectAuthorizeRequest, ConnectAuthorizeResponse>(
            authorized, AccountApiClients.ConnectAuthorizeRoute, request, cancellationToken);

    /// <summary>
    /// Redeems an SSO authorization code for this app's own session (FR-031, ADR-0039).
    /// </summary>
    /// <remarks>
    /// Uses the <em>anonymous</em> client: there is no session yet, and — as with refresh — this call
    /// must not pass through the authorizing handler. Success returns the same
    /// <see cref="SessionResponse"/> a password sign-in does.
    /// </remarks>
    /// <param name="request">The code and its PKCE verifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new session, or the failure.</returns>
    public Task<ApiResult<SessionResponse>> ConnectTokenAsync(
        ConnectTokenRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ConnectTokenRequest, SessionResponse>(
            anonymous, AccountApiClients.ConnectTokenRoute, request, cancellationToken);

    /// <summary>Reads the signed-in user's own profile (FR-012).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The profile, or the failure.</returns>
    public Task<ApiResult<ProfileResponse>> GetProfileAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ProfileResponse>(authorized, new HttpRequestMessage(HttpMethod.Get, $"{Route}/profile"), cancellationToken);

    /// <summary>Updates display name and preferred language (FR-012).</summary>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The updated profile, or the failure.</returns>
    public Task<ApiResult<ProfileResponse>> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProfileResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Put, $"{Route}/profile") { Content = JsonContent.Create(request) },
            cancellationToken);

    /// <summary>Starts a verify-before-swap address change (FR-013).</summary>
    /// <param name="request">The address to move to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome, which is the same whether or not the address is taken.</returns>
    public Task<ApiResult> ChangeEmailAsync(
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(authorized, $"{Route}/change-email", request, cancellationToken);

    /// <summary>Completes an address change from the mail link (FR-013).</summary>
    /// <param name="request">The user id, new address and token from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(authorized, $"{Route}/confirm-email-change", request, cancellationToken);

    /// <summary>Produces a copy of the caller's data (FR-014).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The archive, or the failure.</returns>
    public Task<ApiResult<DataExport>> ExportAsync(CancellationToken cancellationToken = default) =>
        PostAsync<object?, DataExport>(authorized, $"{Route}/export", null, cancellationToken);

    /// <summary>Deletes the account, immediately and irreversibly (FR-015).</summary>
    /// <param name="request">The current password, re-entered.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> DeleteAccountAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync(authorized, $"{Route}/delete", request, cancellationToken);

    /// <summary>Lists the caller's registered passkeys (FR-010).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The passkeys, or the failure.</returns>
    public Task<ApiResult<IReadOnlyList<PasskeyResponse>>> ListPasskeysAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<PasskeyResponse>>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, $"{Route}/passkeys"),
            cancellationToken);

    /// <summary>
    /// Fetches the options for <c>navigator.credentials.create()</c> (FR-010).
    /// </summary>
    /// <remarks>
    /// Returned as raw JSON and handed to the browser untouched: it is the browser's own wire format,
    /// and round-tripping it through a DTO could only lose a field.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The creation options as JSON, or the failure.</returns>
    public Task<ApiResult<string>> PasskeyCreationOptionsAsync(CancellationToken cancellationToken = default) =>
        SendRawAsync(authorized, Ceremony(HttpMethod.Post, $"{Route}/passkeys/options"), cancellationToken);

    /// <summary>Stores a newly created passkey (FR-010).</summary>
    /// <param name="request">The credential produced by the browser, plus its label.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored passkey, or the failure.</returns>
    public Task<ApiResult<PasskeyResponse>> RegisterPasskeyAsync(
        RegisterPasskeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = Ceremony(HttpMethod.Post, $"{Route}/passkeys");
        message.Content = JsonContent.Create(request);
        return SendAsync<PasskeyResponse>(authorized, message, cancellationToken);
    }

    /// <summary>Removes a passkey (FR-010).</summary>
    /// <param name="credentialId">The credential's Base64Url identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> RemovePasskeyAsync(string credentialId, CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            new HttpRequestMessage(HttpMethod.Delete, $"{Route}/passkeys/{Uri.EscapeDataString(credentialId)}"),
            cancellationToken);

    /// <summary>Fetches the options for <c>navigator.credentials.get()</c> (FR-011).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The request options as JSON, or the failure.</returns>
    public Task<ApiResult<string>> PasskeyRequestOptionsAsync(CancellationToken cancellationToken = default) =>
        SendRawAsync(anonymous, Ceremony(HttpMethod.Post, $"{Route}/login/passkey/options"), cancellationToken);

    /// <summary>Signs in with a passkey (FR-011).</summary>
    /// <param name="request">The assertion produced by the browser.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new session, or the failure.</returns>
    public Task<ApiResult<SessionResponse>> PasskeyLoginAsync(
        PasskeyLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = Ceremony(HttpMethod.Post, $"{Route}/login/passkey");
        message.Content = JsonContent.Create(request);
        return SendAsync<SessionResponse>(anonymous, message, cancellationToken);
    }

    /// <summary>
    /// Marks a request as part of a WebAuthn ceremony, which must carry its challenge cookie.
    /// </summary>
    /// <remarks>
    /// Identity keeps the challenge in a cookie between the "options" call and the call that submits
    /// the credential. The client and the API are separate origins (ADR-0013), and a cross-origin
    /// fetch drops cookies unless it asks for them — without this marker the ceremony fails with
    /// "challenge not found", which reads like a passkey problem and is not one.
    /// </remarks>
    private static HttpRequestMessage Ceremony(HttpMethod method, string route)
    {
        var message = new HttpRequestMessage(method, route);
        message.Options.Set(ApiRequestOptions.IncludeCredentials, true);
        return message;
    }

    private static Task<ApiResult> PostAsync<TRequest>(
        HttpClient client,
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendAsync(
            client,
            new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(request) },
            cancellationToken);

    private static Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        HttpClient client,
        string route,
        TRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(
            client,
            new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(request) },
            cancellationToken);

    private static async Task<ApiResult> SendAsync(
        HttpClient client,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        {
            try
            {
                using var response = await client.SendAsync(message, cancellationToken);
                return await ApiResponseReader.ReadAsync(response, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return ApiResult.Failed(Offline);
            }
        }
    }

    private static async Task<ApiResult<T>> SendAsync<T>(
        HttpClient client,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        {
            try
            {
                using var response = await client.SendAsync(message, cancellationToken);
                return await ApiResponseReader.ReadAsync<T>(response, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return ApiResult.Failed<T>(Offline);
            }
        }
    }

    private static async Task<ApiResult<string>> SendRawAsync(
        HttpClient client,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        {
            try
            {
                using var response = await client.SendAsync(message, cancellationToken);
                return await ApiResponseReader.ReadRawJsonAsync(response, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return ApiResult.Failed<string>(Offline);
            }
        }
    }

    private static ApiError Offline => new(ApiFailure.Unreachable);
}
