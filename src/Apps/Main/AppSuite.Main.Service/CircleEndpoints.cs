using System.Security.Claims;
using AppSuite.Application;
using AppSuite.Circles.Contracts;
using AppSuite.Circles.Core.Application;
using AppSuite.Circles.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace AppSuite.Main.Service;

/// <summary>Circle endpoints for the M2 circles slice (FEAT-003).</summary>
/// <remarks>
/// <para>
/// Every route that names a circle carries <see cref="CircleRoleFilter"/> through
/// <see cref="CircleRoleFilterExtensions.RequireCircleRole"/>, and the role beside each one below is
/// the <em>least</em> authority that may call it. The two routes without a circle in their path —
/// creating one, and listing your own — are the only ones that cannot carry it, because there is no
/// circle yet to hold a membership in.
/// </para>
/// <para>
/// The handlers below decide almost nothing. They read the caller, hand the work to the Circles
/// context and translate its refusal into a status code; the rules live in the aggregate, where they
/// can be driven exhaustively without a web server.
/// </para>
/// </remarks>
public static class CircleEndpoints
{
    /// <summary>Maps the circle endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/circles</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapCircleEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // FR-016 — a circle, and the caller's own list of them.
        group.MapPost("/", CreateAsync).WithName("CreateCircle").RequireAuthorization();
        group.MapGet("/", GetMineAsync).WithName("GetMyCircles").RequireAuthorization();

        group.MapGet("/{circleId:guid}", GetAsync)
            .WithName("GetCircle").RequireCircleRole(CircleRole.Member);
        group.MapPut("/{circleId:guid}", UpdateAsync)
            .WithName("UpdateCircle").RequireCircleRole(CircleRole.Admin);
        group.MapDelete("/{circleId:guid}", DeleteAsync)
            .WithName("DeleteCircle").RequireCircleRole(CircleRole.Owner);

        // FR-017 — invitations. The invite and resend routes also take the per-IP limiter: the
        // three limits that matter are keyed on the inviter, the recipient and the circle and are
        // enforced in the handler, but volume from one source is still worth bounding (NFR-005).
        group.MapPost("/{circleId:guid}/invitations", InviteAsync)
            .WithName("InviteToCircle").RequireCircleRole(CircleRole.Admin)
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapGet("/{circleId:guid}/invitations", GetPendingInvitationsAsync)
            .WithName("GetPendingInvitations").RequireCircleRole(CircleRole.Admin);
        group.MapPost("/{circleId:guid}/invitations/{invitationId:guid}/resend", ResendInvitationAsync)
            .WithName("ResendInvitation").RequireCircleRole(CircleRole.Admin)
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapDelete("/{circleId:guid}/invitations/{invitationId:guid}", RevokeInvitationAsync)
            .WithName("RevokeInvitation").RequireCircleRole(CircleRole.Admin);

        // FR-019 — the people who will never sign up.
        group.MapPost("/{circleId:guid}/managed-members", AddManagedMemberAsync)
            .WithName("AddManagedMember").RequireCircleRole(CircleRole.Admin);
        group.MapPut("/{circleId:guid}/managed-members/{membershipId:guid}", UpdateManagedMemberAsync)
            .WithName("UpdateManagedMember").RequireCircleRole(CircleRole.Admin);
        group.MapDelete("/{circleId:guid}/managed-members/{membershipId:guid}", RemoveFromCircleAsync)
            .WithName("RemoveManagedMember").RequireCircleRole(CircleRole.Admin);

        // FR-020 — roles and membership. An admin reaches the removal route; whether they may remove
        // *this* person is the aggregate's answer, because "an admin may not remove another admin"
        // is a rule about two memberships, not about a route.
        group.MapPut("/{circleId:guid}/memberships/{membershipId:guid}/role", ChangeRoleAsync)
            .WithName("ChangeCircleRole").RequireCircleRole(CircleRole.Owner);
        group.MapPost("/{circleId:guid}/memberships/{membershipId:guid}/transfer-ownership", TransferOwnershipAsync)
            .WithName("TransferCircleOwnership").RequireCircleRole(CircleRole.Owner);
        group.MapDelete("/{circleId:guid}/memberships/{membershipId:guid}", RemoveFromCircleAsync)
            .WithName("RemoveFromCircle").RequireCircleRole(CircleRole.Admin);

        // FR-021 — leaving needs no permission, only membership.
        group.MapPost("/{circleId:guid}/leave", LeaveAsync)
            .WithName("LeaveCircle").RequireCircleRole(CircleRole.Member);

        return group;
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateCircleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new CreateCircleCommand(userId, request.Name, request.Label), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/circles/{result.Value.Id}", result.Value)
            : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var circles = await sender.Send(new GetMyCirclesQuery(userId), cancellationToken).ConfigureAwait(false);

        return Results.Ok(circles);
    }

    private static async Task<IResult> GetAsync(
        Guid circleId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new GetCircleQuery(userId, circleId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> UpdateAsync(
        Guid circleId,
        [FromBody] UpdateCircleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new UpdateCircleCommand(userId, circleId, request.Name, request.Label), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    /// <summary>
    /// Deletes a circle (FR-022). No password: the owner types the circle's name back, because the
    /// threat here is the misclick — a hijacked session that wants to hurt you deletes the account,
    /// which is guarded separately.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        Guid circleId,
        [FromBody] DeleteCircleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new DeleteCircleCommand(userId, circleId, request.ConfirmedName), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    /// <summary>
    /// Invites an address (FR-017). Answers <c>202</c> with the same body in every case that is not
    /// about the caller's own permission or rate limit — whether the address has an account, whether
    /// it is already in the circle, and whether it declined recently. Those three are precisely the
    /// facts an inviter must not be able to read off this endpoint (NFR-005).
    /// </summary>
    private static async Task<IResult> InviteAsync(
        Guid circleId,
        [FromBody] InviteRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(
                new InviteToCircleCommand(userId, circleId, request.Email, request.ManagedMembershipId),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? InvitationSent()
            : CircleProblems.From(result.Error!.Value, result.RetryAfter);
    }

    private static async Task<IResult> GetPendingInvitationsAsync(
        Guid circleId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new GetPendingInvitationsQuery(userId, circleId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> ResendInvitationAsync(
        Guid circleId,
        Guid invitationId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new ResendInvitationCommand(userId, circleId, invitationId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? InvitationSent()
            : CircleProblems.From(result.Error!.Value, result.RetryAfter);
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid circleId,
        Guid invitationId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new RevokeInvitationCommand(userId, circleId, invitationId), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    private static async Task<IResult> AddManagedMemberAsync(
        Guid circleId,
        [FromBody] AddManagedMemberRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(
                new AddManagedMemberCommand(userId, circleId, request.DisplayName, request.Language),
                cancellationToken)
            .ConfigureAwait(false);

        // A managed member has no address and no page of their own; where they appear is the
        // circle's people list, so that is what the location points at.
        return result.IsSuccess
            ? Results.Created($"/api/v1/circles/{circleId}", result.Value)
            : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> UpdateManagedMemberAsync(
        Guid circleId,
        Guid membershipId,
        [FromBody] UpdateManagedMemberRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(
                new UpdateManagedMemberCommand(userId, circleId, membershipId, request.DisplayName, request.Language),
                cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid circleId,
        Guid membershipId,
        [FromBody] ChangeRoleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new ChangeMemberRoleCommand(userId, circleId, membershipId, request.Role), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    private static async Task<IResult> TransferOwnershipAsync(
        Guid circleId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new TransferOwnershipCommand(userId, circleId, membershipId), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    private static async Task<IResult> RemoveFromCircleAsync(
        Guid circleId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new RemoveFromCircleCommand(userId, circleId, membershipId), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    private static async Task<IResult> LeaveAsync(
        Guid circleId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new LeaveCircleCommand(userId, circleId), cancellationToken)
            .ConfigureAwait(false);

        return Answer(result);
    }

    /// <summary>The single invitation response — identical whatever the address turned out to be.</summary>
    private static IResult InvitationSent() =>
        Results.Accepted(value: new { message = "The invitation has been sent." });

    private static IResult Answer(CircleResult result) =>
        result.IsSuccess ? Results.NoContent() : CircleProblems.From(result.Error!.Value, result.RetryAfter);
}
