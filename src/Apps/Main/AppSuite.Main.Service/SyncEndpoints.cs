using System.Security.Claims;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Core.Domain;

namespace AppSuite.Main.Service;

/// <summary>The sync surface for the M3 sync-and-claim slice (FEAT-004).</summary>
/// <remarks>
/// <para>
/// The delta exchange is the cycle every trigger runs; the un-claim route is the one out-of-cycle
/// action — removing a group from the account. Claiming a group is its own route and lands with the
/// claim-screen slice.
/// </para>
/// <para>
/// The handler decides nothing: it reads the caller's account from the bearer token and hands the
/// cycle to <see cref="ISyncStore"/>, which owns the sequence, last-write-wins and retention rules.
/// Everything is scoped to that one account — the only scope sync has.
/// </para>
/// </remarks>
public static class SyncEndpoints
{
    /// <summary>Maps the sync endpoints onto a route group.</summary>
    /// <param name="group">The <c>/api/v{version}/sync</c> group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // FR-023 / FR-024 — one delta exchange per cycle: push what is dirty, pull what is above the
        // cursor. A no-op cycle (nothing dirty, cursor current) carries no aggregate bodies either way.
        group.MapPost("/", ExchangeAsync).WithName("Sync").RequireAuthorization();

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
