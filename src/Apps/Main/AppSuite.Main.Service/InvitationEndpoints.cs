using System.Security.Claims;
using AppSuite.Application;
using AppSuite.Circles.Core.Application;

namespace AppSuite.Main.Service;

/// <summary>
/// The invited person's side of an invitation (FR-018, UC-MAIN-010).
/// </summary>
/// <remarks>
/// <para>
/// These routes deliberately sit outside <c>/api/v1/circles</c> and carry no
/// <see cref="CircleRoleFilter"/>: everyone who calls them is, by definition, not yet a member of
/// the circle in question. What admits them instead is a single check made in the aggregate — the
/// caller's <strong>verified</strong> address is the invited address — and it is the same check on
/// both paths, which is why the mailed link is not a bearer key to anyone's circle.
/// </para>
/// <para>
/// <strong>Declining is anonymous</strong>, and that is a real trade-off rather than an oversight:
/// anyone holding a forwarded link can decline an invitation that was not theirs, and the inviter's
/// recourse is to send another. Requiring an account would make "declined" prove the address has
/// one, which is the oracle ADR-0018 paid UX to close. The damage is bounded; the leak would not
/// have been.
/// </para>
/// </remarks>
public static class InvitationEndpoints
{
    /// <summary>Maps the invitation endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/invitations</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // The in-app path: invitations sent to the caller's own verified address. No token from any
        // mail is involved at any point.
        group.MapGet("/", GetJoinRequestsAsync).WithName("GetJoinRequests").RequireAuthorization();
        group.MapPost("/{circleId:guid}/{invitationId:guid}/accept", AcceptJoinRequestAsync)
            .WithName("AcceptJoinRequest").RequireAuthorization();
        group.MapPost("/{circleId:guid}/{invitationId:guid}/decline", DeclineJoinRequestAsync)
            .WithName("DeclineJoinRequest").RequireAuthorization();

        // The mailed-link path. Reading and declining need no account — someone who has never used
        // the product must be able to say no.
        group.MapGet("/{secret}", GetPreviewAsync)
            .WithName("GetInvitationPreview").AllowAnonymous()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapPost("/{secret}/decline", DeclineAsync)
            .WithName("DeclineInvitation").AllowAnonymous()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapPost("/{secret}/accept", AcceptAsync)
            .WithName("AcceptInvitation").RequireAuthorization()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);

        return group;
    }

    private static async Task<IResult> GetJoinRequestsAsync(
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var requests = await sender.Send(new GetJoinRequestsQuery(userId), cancellationToken).ConfigureAwait(false);

        return Results.Ok(requests);
    }

    private static async Task<IResult> AcceptJoinRequestAsync(
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
            .Send(new AcceptJoinRequestCommand(userId, circleId, invitationId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> DeclineJoinRequestAsync(
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
            .Send(new DeclineJoinRequestCommand(userId, circleId, invitationId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.NoContent() : CircleProblems.From(result.Error!.Value);
    }

    /// <summary>
    /// What the holder of a link is shown before deciding: the circle's name, its label and who
    /// invited them. Never the people list — someone deciding whether to join has not joined yet
    /// (NFR-006) — and never whether the invited address has an account.
    /// </summary>
    private static async Task<IResult> GetPreviewAsync(
        string secret,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender
            .Send(new GetInvitationPreviewQuery(secret), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    /// <summary>
    /// Accepts from the mailed link. The link identifies the invitation; it does not authorize
    /// joining — the caller's verified address does, and someone who was forwarded the mail creates
    /// nothing and is told exactly what an unknown link is told (FR-018).
    /// </summary>
    private static async Task<IResult> AcceptAsync(
        string secret,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender
            .Send(new AcceptInvitationCommand(userId, secret), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : CircleProblems.From(result.Error!.Value);
    }

    private static async Task<IResult> DeclineAsync(
        string secret,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender
            .Send(new DeclineInvitationCommand(secret), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.NoContent() : CircleProblems.From(result.Error!.Value);
    }
}
