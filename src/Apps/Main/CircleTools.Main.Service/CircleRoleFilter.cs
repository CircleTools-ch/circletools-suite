using CircleTools.Circles.Core.Domain;

namespace CircleTools.Main.Service;

/// <summary>
/// The role a route requires of the caller in the circle it names (NFR-006).
/// </summary>
/// <param name="Role">The least authority that may call the route.</param>
/// <remarks>
/// Placed on the endpoint as metadata rather than passed to the filter, so it can be read back off
/// the endpoint data source. That is what lets a fitness function walk every route under
/// <c>/api/v1/circles</c> and fail the build when one is added without a guard, instead of relying
/// on a reviewer noticing.
/// </remarks>
public sealed record CircleRoleRequirement(CircleRole Role);

/// <summary>
/// Refuses a circle route the caller may not use, before the handler runs (NFR-006).
/// </summary>
/// <param name="circles">The circle repository port.</param>
/// <remarks>
/// <para>
/// The caller's membership is loaded <strong>per request</strong>. No role ever enters a token, so a
/// removal or a demotion takes effect on the very next request rather than at the next refresh —
/// which is what FR-020's "the effect is immediate" actually means.
/// </para>
/// <para>
/// This does not replace the checks inside the aggregate; the handlers authorize again, and those
/// checks are the enforcement that the unit tests drive exhaustively. What the filter adds is a
/// property that can be verified structurally rather than case by case: <em>every</em> route naming
/// a circle is guarded, including the one somebody adds next year. The cost is loading the circle
/// twice on a permitted request, which is a bounded aggregate — at most a hundred people and fifty
/// invitations — and cheap next to the failure it rules out.
/// </para>
/// </remarks>
public sealed class CircleRoleFilter(ICircleRepository circles) : IEndpointFilter
{
    /// <summary>The route parameter naming the circle.</summary>
    public const string CircleIdRouteParameter = "circleId";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var requirement = http.GetEndpoint()?.Metadata.GetMetadata<CircleRoleRequirement>()
            ?? throw new InvalidOperationException(
                "A circle route was mapped without a CircleRoleRequirement. Use RequireCircleRole.");

        if (!http.User.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }

        if (!http.Request.RouteValues.TryGetValue(CircleIdRouteParameter, out var raw)
            || !Guid.TryParse(raw?.ToString(), out var circleId))
        {
            return CircleProblems.NoSuchCircle();
        }

        var circle = await circles.FindAsync(circleId, http.RequestAborted).ConfigureAwait(false);

        // A circle that does not exist and a circle the caller is not in are answered by the same
        // line of code, which is the cheapest way to keep the two answers identical (NFR-006).
        if (circle is null)
        {
            return CircleProblems.NoSuchCircle();
        }

        var authorized = circle.Authorize(userId, requirement.Role);

        return authorized.IsSuccess
            ? await next(context).ConfigureAwait(false)
            : CircleProblems.From(authorized.Error!.Value);
    }
}

/// <summary>Applies <see cref="CircleRoleFilter"/> to a route.</summary>
public static class CircleRoleFilterExtensions
{
    /// <summary>
    /// Requires the caller to hold at least <paramref name="role"/> in the circle the route names.
    /// </summary>
    /// <param name="builder">The route being mapped.</param>
    /// <param name="role">The least authority that may call it.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder RequireCircleRole(this RouteHandlerBuilder builder, CircleRole role)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new CircleRoleRequirement(role))
            .AddEndpointFilter<CircleRoleFilter>()
            .RequireAuthorization();
    }
}
