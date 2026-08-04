using System.Security.Claims;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Core.Domain;

namespace AppSuite.Main.Service;

/// <summary>The sync surface for the M3 sync-and-claim slice (FEAT-004).</summary>
/// <remarks>
/// <para>
/// The one route here is the delta exchange. Claiming a group and removing one from the account are
/// their own routes and land with the client slice; this slice is the server foundation the two-client
/// convergence tests stand on.
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
}
