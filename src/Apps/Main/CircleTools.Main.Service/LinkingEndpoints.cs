using System.Security.Claims;
using System.Text.Json;
using CircleTools.Circles.Core.Application;
using CircleTools.Circles.Core.Domain;
using CircleTools.Infrastructure.Mail;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Service.Core.Domain;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service;

/// <summary>
/// The M4 linking surface (FEAT-005): the one server-side, per-caller-authorized store ADR-0020
/// sanctioned. It sits beside the opaque M3 sync store, never inside it, and is the only place the
/// Main host understands the Secret-Santa schema.
/// </summary>
/// <remarks>
/// Authorization is the whole game and it fails silently, so the two sharpest rules are made explicit
/// here: a <c>my-assignment</c> request by a stranger to the group is <strong>denied (403), never
/// filtered to an empty result</strong>; and a non-organizer's response carries at most that one
/// caller's pairing. Both decisions are made in <see cref="ILinkingStore"/> against real state and
/// surfaced here as status codes (NFR-002, TP-005).
/// </remarks>
public static class LinkingEndpoints
{
    /// <summary>Maps the member-link endpoints onto the <c>/api/v{version}/member-links</c> group.</summary>
    /// <param name="group">The route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapMemberLinkEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/", SendAsync).WithName("SendMemberLink").RequireAuthorization()
            .RequireRateLimiting(AccountEndpoints.SensitiveRateLimitPolicy);
        group.MapPost("/circle", RegisterCircleLinkAsync).WithName("RegisterCircleLink").RequireAuthorization();
        group.MapPost("/self", SelfLinkAsync).WithName("SelfLinkMember").RequireAuthorization();
        group.MapGet("/pending", GetPendingAsync).WithName("GetPendingMemberLinks").RequireAuthorization();
        group.MapPost("/{id:guid}/accept", AcceptAsync).WithName("AcceptMemberLink").RequireAuthorization();
        group.MapPost("/{id:guid}/decline", DeclineAsync).WithName("DeclineMemberLink").RequireAuthorization();
        group.MapDelete("/{id:guid}", RevokeOrUnlinkAsync).WithName("RevokeOrUnlinkMemberLink").RequireAuthorization();
        group.MapGet("/resolutions", DrainResolutionsAsync).WithName("DrainMemberLinkResolutions").RequireAuthorization();
        group.MapPost("/resolutions/ack", AckResolutionsAsync).WithName("AckMemberLinkResolutions").RequireAuthorization();

        return group;
    }

    /// <summary>Maps the group-scoped linking endpoints onto the <c>/api/v{version}/groups</c> group.</summary>
    /// <param name="group">The route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapGroupLinkingEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{id:guid}/events/{eventId:guid}/my-assignment", GetMyAssignmentAsync).WithName("GetMyAssignment").RequireAuthorization();
        group.MapPut("/{id:guid}/events/{eventId:guid}/my-wishlist", PutMyWishlistAsync).WithName("PutMyWishlist").RequireAuthorization();
        group.MapPost("/{id:guid}/assignments/publish", PublishAssignmentsAsync).WithName("PublishAssignments").RequireAuthorization();
        group.MapPut("/{id:guid}/events", PublishGroupEventsAsync).WithName("PublishGroupEvents").RequireAuthorization();
        group.MapDelete("/{id:guid}/members/{memberId:guid}/link", UnlinkMemberAsync).WithName("UnlinkMember").RequireAuthorization();

        return group;
    }

    /// <summary>Maps <c>GET /api/v{version}/my-assignments</c>.</summary>
    public static RouteGroupBuilder MapMyAssignmentsEndpoint(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/", GetMyAssignmentsAsync).WithName("GetMyAssignments").RequireAuthorization();
        return group;
    }

    /// <summary>Maps <c>GET /api/v{version}/my-wishlists</c>.</summary>
    public static RouteGroupBuilder MapMyWishlistsEndpoint(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/", GetMyWishlistsAsync).WithName("GetMyWishlists").RequireAuthorization();
        return group;
    }

    /// <summary>Maps <c>GET /api/v{version}/my-memberships</c>.</summary>
    public static RouteGroupBuilder MapMyMembershipsEndpoint(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/", GetMyMembershipsAsync).WithName("GetMyMemberships").RequireAuthorization();
        return group;
    }

    private static async Task<IResult> SendAsync(
        SendMemberLinkRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        IAccountDirectory directory,
        IOutbox outbox,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        // Constant-time account lookup, the same path M2 uses (NFR-005): the response is identical
        // whether or not the address has an account.
        var target = await directory.FindByVerifiedEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);

        // A self-link — the organizer linking their own account — auto-accepts with no second step.
        if (target is not null && target.UserId == caller)
        {
            await store.CreateSelfLinkAsync(
                new MemberLinkRecord(
                    Guid.NewGuid(), request.GroupId, request.GroupName, request.MemberId,
                    request.MemberDisplayName, caller, caller, MemberLinkState.Linked),
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(new SendMemberLinkResponse(MemberLinkOutcome.SelfLinked));
        }

        await store.CreateMemberLinkAsync(
            new MemberLinkRecord(
                Guid.NewGuid(), request.GroupId, request.GroupName, request.MemberId,
                request.MemberDisplayName, caller, target?.UserId, MemberLinkState.Pending),
            cancellationToken).ConfigureAwait(false);

        // The mail leaves out of band and only when there is an account to reach; the "request sent"
        // response above says nothing about whether that happened.
        if (target is not null)
        {
            var inviter = await directory.FindAsync(caller, cancellationToken).ConfigureAwait(false);
            await QueueMemberLinkMailAsync(outbox, clock, target, request, inviter?.DisplayName ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        return Results.Accepted(value: new SendMemberLinkResponse(MemberLinkOutcome.RequestSent));
    }

    private static async Task<IResult> SelfLinkAsync(
        SelfLinkRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        // Creating a group makes you a member of it: the organizer links their own verified account with
        // no invitation round-trip and no address — the caller is the target, taken from the bearer token,
        // so nothing here is client-vouched. Same authorization profile as a self-link found through the
        // email path (that too self-links to whatever group the caller names), and idempotent under the
        // one-user-per-group guard.
        await store.CreateSelfLinkAsync(
            new MemberLinkRecord(
                Guid.NewGuid(), request.GroupId, request.GroupName, request.MemberId,
                request.MemberDisplayName, caller, caller, MemberLinkState.Linked),
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> RegisterCircleLinkAsync(
        RegisterCircleLinkRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CircleTools.Application.ISender sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        // Verify the shared-circle claim server-side (the client cannot be trusted to vouch for it): the
        // authorized circle read both proves the caller is a member of the circle and returns its people
        // with their account ids, so the target must appear there too. Anything else is denied.
        var circle = await sender.Send(new GetCircleQuery(caller, request.CircleId), cancellationToken).ConfigureAwait(false);
        if (!circle.IsSuccess
            || !circle.Value!.People.Any(p => p.AccountId == request.TargetAccountId))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        await store.CreateCircleLinkAsync(
            new MemberLinkRecord(
                Guid.NewGuid(), request.GroupId, request.GroupName, request.MemberId,
                request.MemberDisplayName, caller, request.TargetAccountId, MemberLinkState.Linked),
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPendingAsync(
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var pending = await store.GetPendingForTargetAsync(caller, cancellationToken).ConfigureAwait(false);

        return Results.Ok(pending
            .Select(p => new PendingMemberLinkResponse(p.Id, p.GroupId, p.GroupName, p.MemberDisplayName, InviterDisplayName: string.Empty))
            .ToList());
    }

    private static async Task<IResult> AcceptAsync(
        Guid id,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var result = await store.AcceptAsync(id, caller, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            AcceptResult.Linked => Results.Ok(new AcceptMemberLinkResponse(AcceptMemberLinkOutcome.Linked)),
            AcceptResult.AlreadyLinked => Results.Ok(new AcceptMemberLinkResponse(AcceptMemberLinkOutcome.AlreadyLinked)),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> DeclineAsync(
        Guid id,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        await store.DeclineAsync(id, caller, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeOrUnlinkAsync(
        Guid id,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        await store.RevokeOrUnlinkAsync(id, caller, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> DrainResolutionsAsync(
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var resolutions = await store.DrainResolutionsAsync(caller, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new MemberLinkResolutionsResponse(resolutions
            .Select(r => new MemberLinkResolution(
                r.Id, r.GroupId, r.MemberId,
                r.Kind == LinkResolutionKind.Linked ? MemberLinkResolutionKind.Linked : MemberLinkResolutionKind.Unlinked,
                r.UserId))
            .ToList()));
    }

    private static async Task<IResult> AckResolutionsAsync(
        AckMemberLinkResolutionsRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        await store.AckResolutionsAsync(request.ResolutionIds ?? [], caller, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> UnlinkMemberAsync(
        Guid id,
        Guid memberId,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        await store.UnlinkMemberAsync(id, memberId, caller, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMyAssignmentAsync(
        Guid id,
        Guid eventId,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var result = await store.GetMyAssignmentAsync(id, eventId, caller, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            AssignmentReadStatus.Found => Results.Ok(ToResponse(result.Assignment!)),

            // A member the group knows, but no draw yet: nothing to show — not a denial.
            AssignmentReadStatus.NotDrawn => Results.NotFound(),

            // A stranger to the group: denied, never an empty result that reads the same as "no draw".
            _ => Results.StatusCode(StatusCodes.Status403Forbidden),
        };
    }

    private static async Task<IResult> GetMyAssignmentsAsync(
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var views = await store.GetMyAssignmentsAsync(caller, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new MyAssignmentsResponse(views.Select(ToResponse).ToList()));
    }

    private static async Task<IResult> PutMyWishlistAsync(
        Guid id,
        Guid eventId,
        UpdateMyWishlistRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var stored = await store.UpsertMyWishlistAsync(id, eventId, request.EventName ?? string.Empty, caller, request.Items ?? [], cancellationToken).ConfigureAwait(false);

        // Only a linked member of the group owns a wishlist there; a non-member is denied, not silently ignored.
        return stored ? Results.NoContent() : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetMyWishlistsAsync(
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var lists = await store.GetMyWishlistsAsync(caller, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new MyWishlistsResponse(lists
            .Select(w => new MyWishlistResponse(w.GroupId, w.GroupName, w.EventId, w.EventName, w.Items))
            .ToList()));
    }

    private static async Task<IResult> GetMyMembershipsAsync(
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var memberships = await store.GetMyMembershipsAsync(caller, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new MyMembershipsResponse(memberships
            .Select(m => new MyMembership(m.GroupId, m.GroupName, m.EventId, m.EventName))
            .ToList()));
    }

    private static async Task<IResult> PublishAssignmentsAsync(
        Guid id,
        PublishAssignmentsRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var budget = request.Budget is null ? null : new LinkingBudget(request.Budget.Amount, request.Budget.Currency);
        var eventDetails = request.Event is null
            ? null
            : new LinkingEvent(request.Event.Date, request.Event.Location, request.Event.Description);

        var rows = request.Assignments
            .Select(a => new AssignmentPublication(a.GiverUserId, a.RecipientMemberId, a.RecipientName, a.RecipientWishlistSnapshot))
            .ToList();

        await store.PublishAssignmentsAsync(
            id, request.GroupName, request.EventId, request.EventName, caller, request.DrawVersion, budget, eventDetails, rows, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> PublishGroupEventsAsync(
        Guid id,
        PublishGroupEventsRequest request,
        ClaimsPrincipal principal,
        ILinkingStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var caller))
        {
            return Results.Unauthorized();
        }

        var events = (request.Events ?? [])
            .Select(e => new GroupEventInput(e.EventId, e.Name))
            .ToList();

        await store.PublishGroupEventsAsync(id, request.GroupName, caller, events, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static MyAssignmentResponse ToResponse(AssignmentView view) =>
        new(
            view.GroupId,
            view.GroupName,
            view.EventId,
            view.EventName,
            view.RecipientName,
            view.RecipientWishlist,
            view.Budget is null ? null : new BudgetDto(view.Budget.Amount, view.Budget.Currency),
            view.Event is null ? null : new EventDetailsDto(view.Event.Date, view.Event.Location, view.Event.Description));

    private static async Task QueueMemberLinkMailAsync(
        IOutbox outbox,
        TimeProvider clock,
        CircleAccount target,
        SendMemberLinkRequest request,
        string inviterDisplayName,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            email = target.Email,
            language = target.Language,
            groupName = request.GroupName,
            memberDisplayName = request.MemberDisplayName,
            inviterDisplayName,
        });

        await outbox.EnqueueAsync(
            OutboxMessage.Create(MailTemplateRenderer.MemberLinkType, payload, clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }
}
