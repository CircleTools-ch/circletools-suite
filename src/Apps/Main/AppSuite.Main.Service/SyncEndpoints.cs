using System.Security.Claims;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Core.Domain;

namespace AppSuite.Main.Service;

/// <summary>The sync surface for the M3 sync-and-claim slice (FEAT-004).</summary>
/// <remarks>
/// <para>
/// The delta exchange is the cycle every trigger runs; claim and un-claim are the two out-of-cycle
/// actions — the first upload of a group into the account, and removing one from it. After a claim the
/// group is account-scoped and rides the normal cycle like any other; the claim <em>screen</em> that
/// drives this route for a user lands with a later UI slice (FR-025).
/// </para>
/// <para>
/// The handler decides nothing: it reads the caller's account from the bearer token and hands the
/// cycle to <see cref="ISyncStore"/>, which owns the sequence, last-write-wins and retention rules.
/// Everything is scoped to that one account — the only scope sync has.
/// </para>
/// </remarks>
public static class SyncEndpoints
{
    // The claim route is group-specific; Platform still stores the kind opaquely. Kept as a literal
    // here so the host never has to reference the Secret-Santa client for its kind constant.
    private const string GroupKind = "group";

    /// <summary>Maps the sync endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/sync</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // FR-023 / FR-024 — one delta exchange per cycle: push what is dirty, pull what is above the
        // cursor. A no-op cycle (nothing dirty, cursor current) carries no aggregate bodies either way.
        group.MapPost("/", ExchangeAsync).WithName("Sync").RequireAuthorization();

        // FR-025 — claim a group into the account: its first upload. Re-claiming a group that already
        // arrived updates it rather than duplicating it (client-minted ids), so a partly-failed
        // multi-group claim is safe to retry. Afterwards the group rides the normal /sync cycle.
        group.MapPost("/groups/{id:guid}/claim", ClaimAsync).WithName("ClaimGroup").RequireAuthorization();

        // FR-026 — remove a group from the account: delete the server copy (assignments with it) and
        // record an un-claim marker so other devices drop the scope but keep their local copy. It is not
        // a delete, and — requiring a connection — it is not part of the offline cycle.
        group.MapDelete("/groups/{id:guid}", UnclaimAsync).WithName("UnclaimGroup").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ExchangeAsync(
        SyncRequest request,
        ClaimsPrincipal principal,
        ISyncStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        var response = await store.ExchangeAsync(accountId, request, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> ClaimAsync(
        Guid id,
        ClaimGroupRequest request,
        ClaimsPrincipal principal,
        ISyncStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        // Platform stays ignorant of the Secret-Santa schema: the route fixes the kind as "group" (the
        // only aggregate that is ever claimed — a profile is per-account and always synced), and the
        // body travels through as an opaque document (ADR-0016, ADR-0033).
        var change = new SyncChange(GroupKind, id, request.Version, request.Body);
        await store.ClaimAsync(accountId, change, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UnclaimAsync(
        Guid id,
        ClaimsPrincipal principal,
        ISyncStore store,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var accountId))
        {
            return Results.Unauthorized();
        }

        // Idempotent: removing a group already gone elsewhere still returns success, so a client that
        // retries after a lost response never sees an error and never leaves a scope half-flipped.
        await store.UnclaimAsync(accountId, id, cancellationToken);

        return Results.NoContent();
    }
}
