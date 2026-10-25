using System.Security.Claims;
using System.Text.Json;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Groups;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CircleTools.SecretSanta.Service;

/// <summary>
/// The Secret-Santa group store over HTTP (ADR-0086, FEAT-027). A request proves who it is with the bearer (an
/// account), <see cref="OrganizerTokenHeader"/> (the organizer link) or <see cref="DeviceKeyHeader"/> (a device
/// that joined without an account) — headers, so secrets never land in access logs. Routes that grant something
/// answer 404 alike for a missing group and a caller without the permission, so ids can't be probed.
/// </summary>
public static class GroupEndpoints
{
    /// <summary>The request header the organizer link's token travels in.</summary>
    public const string OrganizerTokenHeader = "X-Organizer-Token";

    /// <summary>The request header a device key travels in.</summary>
    public const string DeviceKeyHeader = "X-Device-Key";

    /// <summary>The name of the host's per-IP rate-limit policy for routes usable without an account (NFR-009).</summary>
    public const string AccountFreeRateLimitPolicy = "secretsanta-account-free";

    /// <summary>Maps the routes onto a route group (the host passes <c>/api/v1/secretsanta</c>).</summary>
    /// <param name="group">The route group.</param>
    /// <param name="accountFreeRateLimitPolicy">The host's per-IP policy for account-free writes (NFR-009).</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapSecretSantaGroupEndpoints(this RouteGroupBuilder group, string accountFreeRateLimitPolicy)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Organizer: make online, read, push, delete, links, ownership, participants, reveal tokens.
        group.MapPost("/groups", CreateAsync).WithName("CreateOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapGet("/groups/{groupId:guid}", GetAsync).WithName("GetOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPut("/groups/{groupId:guid}", PushAsync).WithName("PushOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapDelete("/groups/{groupId:guid}", DeleteAsync).WithName("DeleteOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPut("/groups/{groupId:guid}/join-link", SetJoinLinkAsync).WithName("SetOnlineGroupJoinLink").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPost("/groups/{groupId:guid}/organizer-link/rotate", RotateOrganizerLinkAsync).WithName("RotateOnlineGroupOrganizerLink").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPost("/groups/{groupId:guid}/take-over", TakeOverAsync).WithName("TakeOverOnlineGroup").RequireAuthorization();
        group.MapPost("/groups/{groupId:guid}/hand-over", HandOverAsync).WithName("HandOverOnlineGroup").RequireAuthorization();
        group.MapPost("/groups/{groupId:guid}/participants", AddParticipantAsync).WithName("AddOnlineGroupParticipant").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapDelete("/groups/{groupId:guid}/participants/{memberId:guid}", ReleaseAsync).WithName("ReleaseOnlineGroupParticipant").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapGet("/groups/{groupId:guid}/reveal-tokens", GetRevealTokensAsync).WithName("GetOnlineGroupRevealTokens").RequireRateLimiting(accountFreeRateLimitPolicy);

        // Participant: own view, own entry, leave.
        group.MapGet("/groups/{groupId:guid}/me", GetParticipantViewAsync).WithName("GetOnlineGroupParticipantView").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPut("/groups/{groupId:guid}/me/entry", WriteOwnEntryAsync).WithName("WriteOnlineGroupOwnEntry").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapDelete("/groups/{groupId:guid}/me", LeaveAsync).WithName("LeaveOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);

        // Join link: preview and take part.
        group.MapGet("/join/{joinToken}", PreviewJoinAsync).WithName("PreviewOnlineGroupJoin").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPost("/join/{joinToken}", JoinAsync).WithName("JoinOnlineGroup").RequireRateLimiting(accountFreeRateLimitPolicy);

        // Account: every group with a role, invitations.
        group.MapGet("/me/groups", OverviewAsync).WithName("GetMyOnlineGroups").RequireAuthorization();
        group.MapPost("/me/invitations/{groupId:guid}/accept", AcceptAsync).WithName("AcceptOnlineGroupInvitation").RequireAuthorization();
        group.MapPost("/me/invitations/{groupId:guid}/decline", DeclineAsync).WithName("DeclineOnlineGroupInvitation").RequireAuthorization();

        // Reveal link: the derived page, and the holder's own entry (ADR-0084).
        group.MapGet("/reveal/{revealToken}", GetRevealAsync).WithName("GetOnlineGroupReveal").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapGet("/reveal/{revealToken}/entry", GetRevealEntryAsync).WithName("GetOnlineGroupRevealEntry").RequireRateLimiting(accountFreeRateLimitPolicy);
        group.MapPut("/reveal/{revealToken}/entry", WriteRevealEntryAsync).WithName("WriteOnlineGroupRevealEntry").RequireRateLimiting(accountFreeRateLimitPolicy);

        return group;
    }

    private static async Task<IResult> CreateAsync(CreateOnlineGroupRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryReadDocument(request.Document, out var document))
        {
            return Results.BadRequest();
        }

        var (status, links) = await service.CreateAsync(CallerOf(http), request.SchemaVersion, document!, request.CreatorKey, request.JoinEnabled, cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(links) : ToResult(status);
    }

    private static async Task<IResult> GetAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        var read = await service.GetForOrganizerAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false);
        if (read is not { } found)
        {
            return Results.NotFound();
        }

        var (details, caller, links) = found;
        return Results.Ok(new OrganizerGroupView(
            details.Group.GroupId,
            details.Group.Version,
            details.Group.SchemaVersion,
            ToElement(details.Group.Document),
            details.Group.JoinEnabled,
            [.. details.Assignments.Select(a => new GroupRoleView(
                Name(a.Role), Name(a.PrincipalKind), a.MemberId, a.State == RoleAssignmentState.Invited, Name(a.Route)))],
            [.. details.Entries.Select(e => e.MemberId)],
            GroupPermissionNames.Of(caller.Permissions),
            links));
    }

    private static async Task<IResult> PushAsync(Guid groupId, PushOnlineGroupRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryReadDocument(request.Document, out var document))
        {
            return Results.BadRequest();
        }

        var result = await service.PushAsync(groupId, CallerOf(http), request.BaseVersion, request.SchemaVersion, document!, cancellationToken).ConfigureAwait(false);
        return result.Status == WriteStatus.Applied
            ? Results.Ok(new PushOnlineGroupResponse(result.Version, result.Rewritten, ToElement(result.Document!)))
            : ToResult(result.Status);
    }

    private static async Task<IResult> DeleteAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        ToResult(await service.DeleteAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> SetJoinLinkAsync(Guid groupId, JoinLinkRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (status, links) = await service.SetJoinLinkAsync(groupId, CallerOf(http), request.Enabled, request.Rotate, cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(links) : ToResult(status);
    }

    private static async Task<IResult> RotateOrganizerLinkAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        var (status, links) = await service.RotateOrganizerLinkAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(links) : ToResult(status);
    }

    private static async Task<IResult> TakeOverAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        ToResult(await service.TakeOverAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> HandOverAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        var (status, links) = await service.HandOverAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(links) : ToResult(status);
    }

    private static async Task<IResult> AddParticipantAsync(Guid groupId, AddParticipantRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = await service.AddParticipantAsync(groupId, CallerOf(http), request, DisplayName(http.User), cancellationToken).ConfigureAwait(false);

        // "Invitation sent" reads the same whether or not the address has an account (NFR-005).
        return status == WriteStatus.Applied ? Results.Accepted() : ToResult(status);
    }

    private static async Task<IResult> ReleaseAsync(Guid groupId, Guid memberId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        ToResult(await service.ReleaseAsync(groupId, CallerOf(http), memberId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetRevealTokensAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        var tokens = await service.GetRevealTokensAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false);
        return tokens is null ? Results.NotFound() : Results.Ok(tokens);
    }

    private static async Task<IResult> GetParticipantViewAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        var view = await service.GetParticipantViewAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> WriteOwnEntryAsync(Guid groupId, OwnEntryRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (status, version) = await service.WriteOwnEntryAsync(groupId, CallerOf(http), request, cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(new OwnEntryResponse(version)) : ToResult(status);
    }

    private static async Task<IResult> LeaveAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        ToResult(await service.LeaveAsync(groupId, CallerOf(http), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> PreviewJoinAsync(string joinToken, GroupService service, CancellationToken cancellationToken)
    {
        var preview = await service.PreviewJoinAsync(joinToken, cancellationToken).ConfigureAwait(false);
        return preview is null ? Results.NotFound() : Results.Ok(preview);
    }

    private static async Task<IResult> JoinAsync(string joinToken, JoinRequest request, HttpContext http, GroupService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (status, response) = await service.JoinAsync(joinToken, CallerOf(http), request, cancellationToken).ConfigureAwait(false);
        return status == WriteStatus.Applied ? Results.Ok(response) : ToResult(status);
    }

    private static async Task<IResult> OverviewAsync(HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        CallerOf(http).AccountId is { } account
            ? Results.Ok(await service.OverviewAsync(account, cancellationToken).ConfigureAwait(false))
            : Results.Unauthorized();

    private static Task<IResult> AcceptAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        AnswerAsync(groupId, http, service, accept: true, cancellationToken);

    private static Task<IResult> DeclineAsync(Guid groupId, HttpContext http, GroupService service, CancellationToken cancellationToken) =>
        AnswerAsync(groupId, http, service, accept: false, cancellationToken);

    private static async Task<IResult> AnswerAsync(Guid groupId, HttpContext http, GroupService service, bool accept, CancellationToken cancellationToken) =>
        CallerOf(http).AccountId is { } account
            ? ToResult(await service.AnswerInvitationAsync(account, groupId, accept, cancellationToken).ConfigureAwait(false))
            : Results.Unauthorized();

    private static async Task<IResult> GetRevealAsync(string revealToken, GroupService service, CancellationToken cancellationToken)
    {
        var page = await service.GetRevealAsync(revealToken, cancellationToken).ConfigureAwait(false);
        return page is null ? Results.NotFound() : Results.Ok(page);
    }

    private static async Task<IResult> GetRevealEntryAsync(string revealToken, GroupService service, CancellationToken cancellationToken)
    {
        var entry = await service.GetRevealEntryAsync(revealToken, cancellationToken).ConfigureAwait(false);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    private static async Task<IResult> WriteRevealEntryAsync(string revealToken, RevealEntry request, GroupService service, CancellationToken cancellationToken) =>
        ToResult(await service.WriteRevealEntryAsync(revealToken, request, cancellationToken).ConfigureAwait(false));

    // ---------------------------------------------------------------- helpers

    private static Caller CallerOf(HttpContext http)
    {
        var value = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        Guid? account = Guid.TryParse(value, out var id) ? id : null;
        return new Caller(account, Header(http, OrganizerTokenHeader), Header(http, DeviceKeyHeader));
    }

    private static string? Header(HttpContext http, string name)
    {
        var value = http.Request.Headers[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string DisplayName(ClaimsPrincipal user)
    {
        var name = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");
        return string.IsNullOrWhiteSpace(name) ? "Someone" : name;
    }

    private static bool TryReadDocument(JsonElement element, out GroupState? document)
    {
        document = null;
        return element.ValueKind == JsonValueKind.Object && GroupDocument.TryDeserialize(element.GetRawText(), out document);
    }

    private static JsonElement ToElement(GroupState document)
    {
        using var parsed = JsonDocument.Parse(GroupDocument.Serialize(document));
        return parsed.RootElement.Clone();
    }

    private static string Name<T>(T value)
        where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static IResult ToResult(WriteStatus status) => status switch
    {
        WriteStatus.Applied => Results.NoContent(),
        WriteStatus.NotFound => Results.NotFound(),
        WriteStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        WriteStatus.Invalid => Results.BadRequest(),
        WriteStatus.Conflict => Results.Conflict(),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
    };
}
