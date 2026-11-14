// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using CircleTools.ApiClient;
using CircleTools.Circles.Contracts;
using CircleTools.Ui.Account.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Ui.Api;

/// <summary>
/// The typed client over <c>/api/v1/circles</c> and <c>/api/v1/invitations</c> (FEAT-003).
/// </summary>
/// <remarks>
/// <para>
/// Shaped like <see cref="AccountApiClient"/>: expected failures are <see cref="ApiResult"/> values,
/// never exceptions, and a dropped connection becomes <see cref="ApiFailure.Unreachable"/> rather
/// than a crash.
/// </para>
/// <para>
/// <strong>Two of the invitation routes go through the anonymous client on purpose.</strong>
/// Previewing and declining an invitation are things a person does from a mail link before they have
/// an account at all (FR-018). Sending a bearer token there would be harmless but misleading; more
/// importantly, these two must keep working when there is no token to send.
/// </para>
/// </remarks>
/// <param name="authorized">The client that carries the bearer token.</param>
/// <param name="anonymous">The client used for the two routes that precede any session.</param>
public sealed class CirclesApiClient(
    [FromKeyedServices(AccountApiClients.Authorized)] HttpClient authorized,
    [FromKeyedServices(AccountApiClients.Anonymous)] HttpClient anonymous)
{
    private const string Circles = MainApiRoutes.CirclesRoute;
    private const string Invitations = MainApiRoutes.InvitationsRoute;

    /// <summary>Lists the circles the caller belongs to (FR-016).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The caller's circles, or the failure.</returns>
    public Task<ApiResult<IReadOnlyList<CircleSummaryResponse>>> GetMineAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<CircleSummaryResponse>>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, Circles),
            cancellationToken);

    /// <summary>Creates a circle, with the caller as its owner (FR-016).</summary>
    /// <param name="request">The name and label.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new circle, or the failure.</returns>
    public Task<ApiResult<CircleDetailResponse>> CreateAsync(
        CreateCircleRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<CircleDetailResponse>(authorized, Post(Circles, request), cancellationToken);

    /// <summary>Reads one circle and its people (FR-016, FR-020).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The circle, or the failure — <c>404</c> both when it does not exist and when the
    /// caller is not in it (NFR-006).</returns>
    public Task<ApiResult<CircleDetailResponse>> GetAsync(
        Guid circleId,
        CancellationToken cancellationToken = default) =>
        SendAsync<CircleDetailResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, $"{Circles}/{circleId}"),
            cancellationToken);

    /// <summary>Renames a circle or changes its label (FR-020). Owner and admin.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="request">The new name and label.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The updated circle, or the failure.</returns>
    public Task<ApiResult<CircleDetailResponse>> UpdateAsync(
        Guid circleId,
        UpdateCircleRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<CircleDetailResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Put, $"{Circles}/{circleId}")
            {
                Content = JsonContent.Create(request),
            },
            cancellationToken);

    /// <summary>Deletes a circle and everything in it (FR-022). Owner only.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="request">The circle's name, typed back by the owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// A body on a <c>DELETE</c> is unusual, and it is deliberate: the confirmation is what
    /// distinguishes this from a misclick, so it belongs to the request that performs it rather than
    /// to a preceding one that could be skipped (FR-022).
    /// </remarks>
    public Task<ApiResult> DeleteAsync(
        Guid circleId,
        DeleteCircleRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            new HttpRequestMessage(HttpMethod.Delete, $"{Circles}/{circleId}")
            {
                Content = JsonContent.Create(request),
            },
            cancellationToken);

    /// <summary>Leaves a circle (FR-021).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome; an owner is refused with the code that says which way out they have.</returns>
    public Task<ApiResult> LeaveAsync(Guid circleId, CancellationToken cancellationToken = default) =>
        SendAsync(authorized, Post($"{Circles}/{circleId}/leave"), cancellationToken);

    /// <summary>Invites an address to a circle (FR-017). Owner and admin.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="request">The address, and the managed member it stands in for when linking.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The outcome. Success is deliberately uninformative: it says the same thing whether a mail
    /// went out, the address is already in the circle, or a decline is still in its cooldown
    /// (NFR-005).
    /// </returns>
    public Task<ApiResult> InviteAsync(
        Guid circleId,
        InviteRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(authorized, Post($"{Circles}/{circleId}/invitations", request), cancellationToken);

    /// <summary>Lists a circle's outstanding invitations (FR-017). Owner and admin.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending invitations, or the failure.</returns>
    public Task<ApiResult<IReadOnlyList<PendingInvitationResponse>>> GetPendingInvitationsAsync(
        Guid circleId,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<PendingInvitationResponse>>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, $"{Circles}/{circleId}/invitations"),
            cancellationToken);

    /// <summary>Issues a fresh link for an outstanding invitation (FR-017).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome. The previous link stops working.</returns>
    public Task<ApiResult> ResendInvitationAsync(
        Guid circleId,
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            Post($"{Circles}/{circleId}/invitations/{invitationId}/resend"),
            cancellationToken);

    /// <summary>Withdraws an outstanding invitation (FR-017).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> RevokeInvitationAsync(
        Guid circleId,
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            new HttpRequestMessage(HttpMethod.Delete, $"{Circles}/{circleId}/invitations/{invitationId}"),
            cancellationToken);

    /// <summary>Adds someone who will never have an account (FR-019). Owner and admin.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="request">Their display name and optional language.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new person, or the failure.</returns>
    public Task<ApiResult<CirclePersonResponse>> AddManagedMemberAsync(
        Guid circleId,
        AddManagedMemberRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<CirclePersonResponse>(
            authorized,
            Post($"{Circles}/{circleId}/managed-members", request),
            cancellationToken);

    /// <summary>Edits a managed member's profile (FR-019).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="membershipId">The managed membership.</param>
    /// <param name="request">The new display name and language.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The updated person, or the failure.</returns>
    public Task<ApiResult<CirclePersonResponse>> UpdateManagedMemberAsync(
        Guid circleId,
        Guid membershipId,
        UpdateManagedMemberRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<CirclePersonResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Put, $"{Circles}/{circleId}/managed-members/{membershipId}")
            {
                Content = JsonContent.Create(request),
            },
            cancellationToken);

    /// <summary>Changes someone's role (FR-020). Owner only, and never to owner.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="membershipId">The membership.</param>
    /// <param name="request">The new role.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> ChangeRoleAsync(
        Guid circleId,
        Guid membershipId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            new HttpRequestMessage(HttpMethod.Put, $"{Circles}/{circleId}/memberships/{membershipId}/role")
            {
                Content = JsonContent.Create(request),
            },
            cancellationToken);

    /// <summary>Hands the circle to another member (FR-020). Owner only.</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="membershipId">The member receiving it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome. The previous owner becomes an admin in the same change.</returns>
    public Task<ApiResult> TransferOwnershipAsync(
        Guid circleId,
        Guid membershipId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            Post($"{Circles}/{circleId}/memberships/{membershipId}/transfer-ownership"),
            cancellationToken);

    /// <summary>Removes someone from a circle (FR-020).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="membershipId">The membership to end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> RemoveAsync(
        Guid circleId,
        Guid membershipId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            new HttpRequestMessage(HttpMethod.Delete, $"{Circles}/{circleId}/memberships/{membershipId}"),
            cancellationToken);

    /// <summary>Lists the join requests waiting for the caller's verified address (FR-018).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending join requests, or the failure.</returns>
    /// <remarks>
    /// This is the in-app half of FR-018 and carries no token from any mail: what admits the caller
    /// is that their <em>verified</em> address is the invited one.
    /// </remarks>
    public Task<ApiResult<IReadOnlyList<JoinRequestResponse>>> GetJoinRequestsAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<JoinRequestResponse>>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, Invitations),
            cancellationToken);

    /// <summary>Reads what a mail link points at, without signing in (FR-018).</summary>
    /// <param name="token">The secret from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The circle and inviter to show, or the failure.</returns>
    public Task<ApiResult<InvitationPreviewResponse>> PreviewInvitationAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        SendAsync<InvitationPreviewResponse>(
            anonymous,
            new HttpRequestMessage(HttpMethod.Get, $"{Invitations}/{Uri.EscapeDataString(token)}"),
            cancellationToken);

    /// <summary>
    /// Reads the offline profiles the signed-in recipient of a mail-link invitation may join as
    /// (FR-019). Authorized: unlocked only when the caller's verified address is the invited one.
    /// </summary>
    /// <param name="token">The secret from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preselected target or the offline options, or the failure.</returns>
    public Task<ApiResult<JoinTargetsResponse>> GetInvitationTargetsAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        SendAsync<JoinTargetsResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, $"{Invitations}/{Uri.EscapeDataString(token)}/targets"),
            cancellationToken);

    /// <summary>Accepts an invitation from a mail link (FR-018). Signed in and verified.</summary>
    /// <param name="token">The secret from the link.</param>
    /// <param name="managedMembershipId">
    /// The offline profile the recipient chose to join as, or null to join as new (FR-019). Ignored
    /// when the inviter already aimed the invitation at a managed member.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome; refused unless the caller's verified address is the invited one.</returns>
    public Task<ApiResult> AcceptInvitationAsync(
        string token,
        Guid? managedMembershipId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            Post(
                $"{Invitations}/{Uri.EscapeDataString(token)}/accept",
                new AcceptInvitationBody(managedMembershipId)),
            cancellationToken);

    /// <summary>Declines an invitation from a mail link (FR-018). Anonymous.</summary>
    /// <param name="token">The secret from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// Anonymous on purpose: requiring an account here would make "declined" prove the address has
    /// one, which is the oracle ADR-0018 paid UX to avoid. Anyone holding a forwarded link can
    /// decline it, and that is the accepted cost.
    /// </remarks>
    public Task<ApiResult> DeclineInvitationAsync(string token, CancellationToken cancellationToken = default) =>
        SendAsync(
            anonymous,
            Post($"{Invitations}/{Uri.EscapeDataString(token)}/decline"),
            cancellationToken);

    /// <summary>
    /// Reads the offline profiles the signed-in recipient of an in-app join request may join as
    /// (FR-019). Authorized: unlocked only when the caller's verified address is the invited one.
    /// </summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preselected target or the offline options, or the failure.</returns>
    public Task<ApiResult<JoinTargetsResponse>> GetJoinRequestTargetsAsync(
        Guid circleId,
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        SendAsync<JoinTargetsResponse>(
            authorized,
            new HttpRequestMessage(HttpMethod.Get, $"{Invitations}/{circleId}/{invitationId}/targets"),
            cancellationToken);

    /// <summary>Accepts a join request from inside the app (FR-018).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <param name="managedMembershipId">
    /// The offline profile the recipient chose to join as, or null to join as new (FR-019). Ignored
    /// when the inviter already aimed the invitation at a managed member.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> AcceptJoinRequestAsync(
        Guid circleId,
        Guid invitationId,
        Guid? managedMembershipId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            authorized,
            Post(
                $"{Invitations}/{circleId}/{invitationId}/accept",
                new AcceptInvitationBody(managedMembershipId)),
            cancellationToken);

    /// <summary>Declines a join request from inside the app (FR-018).</summary>
    /// <param name="circleId">The circle.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult> DeclineJoinRequestAsync(
        Guid circleId,
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(authorized, Post($"{Invitations}/{circleId}/{invitationId}/decline"), cancellationToken);

    private static HttpRequestMessage Post(string route) => new(HttpMethod.Post, route);

    private static HttpRequestMessage Post<TRequest>(string route, TRequest request) =>
        new(HttpMethod.Post, route) { Content = JsonContent.Create(request) };

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

    private static ApiError Offline => new(ApiFailure.Unreachable);
}
