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

        // Resolve a token to the group + capability. A wrong/disabled token is an indistinguishable 404.
        group.MapGet("/{token}", GetAsync).WithName("GetSharedGroup");

        // Push a new body (admin capability). Rate-limited.
        group.MapPut("/{token}", PushAsync).WithName("PushSharedGroup")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Disable the current admin link and mint a fresh one.
        group.MapPost("/{token}/rotate-admin", RotateAdminAsync).WithName("RotateSharedGroupAdmin")
            .RequireRateLimiting(AccountFreeRateLimitPolicy);

        // Delete the online copy and all its tokens (owner-less delete, NFR-010).
        group.MapDelete("/{token}", DeleteAsync).WithName("DeleteSharedGroup");

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateSharedGroupRequest request,
        ISharedGroupStore store,
        CancellationToken cancellationToken)
    {
        var links = await store.CreateAsync(request, cancellationToken);
        return links is null
            ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
            : Results.Ok(links);
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
}
