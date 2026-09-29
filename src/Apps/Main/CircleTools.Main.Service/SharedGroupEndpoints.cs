using System.Security.Claims;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service;

/// <summary>
/// The account-free, shared-by-link group surface (ADR-0058, FEAT-013). Unlike every other write
/// surface on this host these routes are <strong>not</strong> bearer-authorized: the share token is
/// the credential, exactly as a reveal link's fragment is. Guessing is defeated by the token's 256-bit
/// entropy (NFR-009), not by identity, so an attacker cannot enumerate groups; the create/push routes
/// are additionally rate-limited per IP and size-capped to bound the account-free write path.
/// </summary>
public static class SharedGroupEndpoints
{
    /// <summary>The per-IP rate-limit policy the account-free write routes carry (NFR-009).</summary>
    public const string AccountFreeRateLimitPolicy = "shared-account-free-write";

    /// <summary>Maps the shared-by-link endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/shared</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapSharedGroupEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Upload a group into shared-by-link storage and mint its links. Rate-limited: this is the
        // anonymous create path (NFR-009).
        group.MapPost("/", CreateAsync).WithName("CreateSharedGroup")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // List the groups the signed-in account owns, so a second device discovers them (D2). A literal
        // segment, so it takes precedence over the "/{token}" route below.
        group.MapGet("/owned", ListOwnedAsync).WithName("ListOwnedSharedGroups").RequireAuthorization();

        // Resolve a token to the group + capability. A wrong/disabled token is an indistinguishable 404.
        // Rate-limited: a successful resolve stamps LastActivityAt (a DB write), so an unthrottled read is
        // a write-amplification vector for any valid-token holder (NFR-009).
        group.MapGet("/{token}", GetAsync).WithName("GetSharedGroup")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Push a new body (admin capability). Rate-limited.
        group.MapPut("/{token}", PushAsync).WithName("PushSharedGroup")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Disable the current admin link and mint a fresh one.
        group.MapPost("/{token}/rotate-admin", RotateAdminAsync).WithName("RotateSharedGroupAdmin")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Delete the online copy and all its tokens (owner-less delete, NFR-010). Rate-limited: an
        // anonymous, account-free mutation like the others (NFR-009).
        group.MapDelete("/{token}", DeleteAsync).WithName("DeleteSharedGroup")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Which members are already claimed (join or admin token) — so the join UI can lock them.
        group.MapGet("/{token}/claims", GetClaimsAsync).WithName("GetSharedGroupClaims");

        // Claim a member ("this is me") through the join link. Rate-limited (account-free write).
        group.MapPost("/{token}/claims", ClaimPersonAsync).WithName("ClaimSharedGroupPerson")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Release a claimed member (admin only) — the sole recovery path. Rate-limited (account-free write).
        group.MapDelete("/{token}/claims/{memberId:guid}", ReleasePersonAsync).WithName("ReleaseSharedGroupPerson")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Claim admin into an account (Shared by link → My account) — bearer-authorized (the claimer
        // signs in). Retires the admin link; the join link keeps working (ADR-0058).
        group.MapPost("/{token}/claim-admin", ClaimAdminAsync).WithName("ClaimSharedGroupAdmin").RequireAuthorization();

        // Release an account-owned group back to shared-by-link (My account → Shared by link) —
        // bearer-authorized, keyed by group id (the admin token is retired by then).
        group.MapPost("/{groupId:guid}/release-to-link", ReleaseAdminAsync).WithName("ReleaseSharedGroupAdmin").RequireAuthorization();

        // Owner read/write of a claimed (My account) group (D2) — the owner has no admin token, so they
        // address the group by id and authorize with their bearer.
        group.MapGet("/{groupId:guid}/owned", GetOwnedAsync).WithName("GetOwnedSharedGroup").RequireAuthorization();
        group.MapPut("/{groupId:guid}/owned", PushOwnedAsync).WithName("PushOwnedSharedGroup").RequireAuthorization();

        // Live reveal links (ADR-0068, FEAT-020). Publish the per-giver reveal slices for a group: by
        // admin token (shared-by-link) or by owner bearer (account mode). Read one giver's slice by its
        // reveal token — the public reveal page's only call, anonymous. All account-free (NFR-009).
        group.MapPut("/{token}/reveal-slices", PublishRevealSlicesAsync).WithName("PublishSharedGroupRevealSlices")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);
        group.MapPut("/{groupId:guid}/owned/reveal-slices", PublishOwnedRevealSlicesAsync).WithName("PublishOwnedSharedGroupRevealSlices").RequireAuthorization();
        // A resolve stamps LastActivityAt (a DB write), so rate-limit it like the token read (NFR-009).
        group.MapGet("/reveal/{revealToken}", GetRevealSliceAsync).WithName("GetSharedGroupRevealSlice")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateSharedGroupRequest request,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var result = await store.CreateAsync(request, cancellationToken);
        return result.Status switch
        {
            CreateSharedGroupStatus.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            // A create that is not the group's creator (or targets an account-owned group) is refused
            // with the same 403 whatever the reason — no oracle for "does this group id exist" (ADR-0058).
            CreateSharedGroupStatus.Refused => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => Results.Ok(result.Links),
        };
    }

    private static async Task<IResult> GetAsync(
        string token,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var view = await store.GetAsync(token, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> PushAsync(
        string token,
        PushSharedGroupRequest request,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var result = await store.PushAsync(token, request.Version, request.Body, cancellationToken);
        return result.Status switch
        {
            SharedWriteStatus.NotFound => Results.NotFound(),
            SharedWriteStatus.Forbidden => Results.Forbid(),
            SharedWriteStatus.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            _ => Results.Ok(new PushSharedGroupResult(result.Version, result.Status == SharedWriteStatus.Collision)),
        };
    }

    private static async Task<IResult> RotateAdminAsync(
        string token,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var rotated = await store.RotateAdminAsync(token, cancellationToken);
        return rotated is null ? Results.Forbid() : Results.Ok(rotated);
    }

    private static async Task<IResult> DeleteAsync(
        string token,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAsync(token, cancellationToken);
        return deleted ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> GetClaimsAsync(
        string token,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var claimed = await store.GetClaimedMembersAsync(token, cancellationToken);
        return claimed is null ? Results.NotFound() : Results.Ok(new ClaimedMembers(claimed));
    }

    private static async Task<IResult> ClaimPersonAsync(
        string token,
        ClaimPersonRequest request,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var result = await store.ClaimPersonAsync(token, request, cancellationToken);
        return result.Status switch
        {
            PersonClaimStatus.NotFound => Results.NotFound(),
            PersonClaimStatus.AlreadyClaimed => Results.Ok(new PersonClaimResponse(false, true)),
            _ => Results.Ok(new PersonClaimResponse(true, false)),
        };
    }

    private static async Task<IResult> ReleasePersonAsync(
        string token,
        Guid memberId,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var released = await store.ReleasePersonAsync(token, memberId, cancellationToken);
        return released ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> ClaimAdminAsync(
        string token,
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var claimed = await store.ClaimAdminAsync(token, accountId, principal.DisplayName(), cancellationToken);
        return claimed ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> ReleaseAdminAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var rotated = await store.ReleaseAdminAsync(accountId, groupId, cancellationToken);
        return rotated is null ? Results.Forbid() : Results.Ok(rotated);
    }

    private static async Task<IResult> ListOwnedAsync(
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var owned = await store.ListOwnedAsync(accountId, cancellationToken);
        return Results.Ok(new OwnedSharedGroups(owned));
    }

    private static async Task<IResult> GetOwnedAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var view = await store.GetAsOwnerAsync(accountId, groupId, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> PushOwnedAsync(
        Guid groupId,
        PushSharedGroupRequest request,
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var result = await store.PushAsOwnerAsync(accountId, groupId, request.Version, request.Body, cancellationToken);
        return result.Status switch
        {
            SharedWriteStatus.NotFound => Results.NotFound(),
            SharedWriteStatus.Forbidden => Results.Forbid(),
            SharedWriteStatus.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            _ => Results.Ok(new PushSharedGroupResult(result.Version, result.Status == SharedWriteStatus.Collision)),
        };
    }

    private static async Task<IResult> PublishRevealSlicesAsync(
        string token,
        PublishRevealSlicesRequest request,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stored = await store.SetRevealSlicesAsync(token, request.Slices, cancellationToken);
        return stored ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> PublishOwnedRevealSlicesAsync(
        Guid groupId,
        PublishRevealSlicesRequest request,
        ClaimsPrincipal principal,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var stored = await store.SetRevealSlicesAsOwnerAsync(accountId, groupId, request.Slices, cancellationToken);
        return stored ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> GetRevealSliceAsync(
        string revealToken,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var view = await store.GetRevealSliceAsync(revealToken, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
