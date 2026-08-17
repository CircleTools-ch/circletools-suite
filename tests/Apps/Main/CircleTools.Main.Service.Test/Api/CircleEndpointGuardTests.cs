using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// NFR-006's "an endpoint added later" criterion, as a fitness function over the endpoint data
/// source rather than as a review habit.
/// </summary>
/// <remarks>
/// <para>
/// Every other authorization test in this milestone asserts something about the routes that exist
/// today. This one asserts a property of the <em>next</em> route: a circle route mapped without
/// <see cref="CircleRoleFilterExtensions.RequireCircleRole"/> fails the build. That is why
/// <see cref="CircleRoleRequirement"/> is endpoint metadata instead of a filter argument — metadata
/// can be read back off the data source, a closure cannot.
/// </para>
/// <para>
/// The assertion is on the metadata, not on the filter instance: ASP.NET keeps endpoint filters in
/// an opaque pipeline that cannot be enumerated, and <c>RequireCircleRole</c> is the only thing in
/// the solution that attaches the requirement — it adds the metadata and the filter together, so one
/// cannot appear without the other.
/// </para>
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class CircleEndpointGuardTests(MailApiFactory factory)
{
    /// <summary>
    /// Routes that name a circle and deliberately carry <strong>no</strong> role requirement, with
    /// the reason each one has to.
    /// </summary>
    /// <remarks>
    /// These are the in-app invitation routes (FR-018). Their whole purpose is to serve somebody who
    /// is <em>not in the circle yet</em>, and <see cref="CircleRoleFilter"/> would answer them
    /// <c>404</c> — the correct answer for a non-member, and the wrong one for an invitee. What
    /// admits them instead is the verified-address check in the handler, covered by
    /// <c>Circles.Core.Test.HandlerTests</c> and <see cref="CircleAuthorizationTests"/>.
    /// <para>
    /// Adding a name here is a decision about somebody else's data. It is meant to be an
    /// uncomfortable one, and <see cref="EveryExemptedRouteStillExists"/> keeps the list from
    /// outliving its routes.
    /// </para>
    /// </remarks>
    private static readonly string[] _mayNameACircleWithoutARoleRequirement =
    [
        "GetJoinRequests",
        "GetJoinRequestTargets",
        "AcceptJoinRequest",
        "DeclineJoinRequest",
    ];

    /// <summary>
    /// The load-bearing assertion: every route under <c>/api/v1/circles</c> that names a circle
    /// requires a role in it.
    /// </summary>
    [Fact]
    public void EveryCircleRoute_CarriesTheRoleFilter()
    {
        var unguarded = CircleRoutes()
            .Where(NamesACircle)
            .Where(endpoint => endpoint.Metadata.GetMetadata<CircleRoleRequirement>() is null)
            .Select(Describe)
            .ToList();

        Assert.Empty(unguarded);
    }

    /// <summary>
    /// The two routes that cannot carry the filter — creating a circle and listing your own — still
    /// may not be anonymous. Without this, "no role requirement" would be a way to make a route
    /// public by omission rather than by decision.
    /// </summary>
    [Fact]
    public void ACircleRouteWithoutARoleRequirement_StillRequiresAnAccount()
    {
        var open = CircleRoutes()
            .Where(endpoint => endpoint.Metadata.GetMetadata<CircleRoleRequirement>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
                || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .ToList();

        Assert.Empty(open);
    }

    /// <summary>
    /// The same rule reaches outside the circles group: any route anywhere that takes a
    /// <c>circleId</c> is either guarded or on the exemption list above. Scoping the scan to
    /// <c>/api/v1/circles</c> alone would have missed the invitation routes entirely — which are
    /// precisely the ones that had to be reasoned about.
    /// </summary>
    [Fact]
    public void EveryRouteNamingACircleAnywhere_IsGuardedOrExempted()
    {
        var unaccounted = Endpoints()
            .Where(NamesACircle)
            .Where(endpoint => endpoint.Metadata.GetMetadata<CircleRoleRequirement>() is null)
            .Where(endpoint => !_mayNameACircleWithoutARoleRequirement.Contains(NameOf(endpoint), StringComparer.Ordinal))
            .Select(Describe)
            .ToList();

        Assert.Empty(unaccounted);
    }

    /// <summary>
    /// Keeps the exemption list honest: a name that matches no route is an exemption that has
    /// silently stopped meaning anything.
    /// </summary>
    [Fact]
    public void EveryExemptedRouteStillExists()
    {
        var names = Endpoints().Select(NameOf).ToList();

        Assert.All(
            _mayNameACircleWithoutARoleRequirement,
            exempted => Assert.Contains(exempted, names, StringComparer.Ordinal));
    }

    /// <summary>
    /// Guards the scan itself. Every assertion above is vacuously true against an empty sequence, so
    /// a change to the route patterns that stopped the filter matching would read as "all green"
    /// rather than as "nothing was checked".
    /// </summary>
    [Fact]
    public void TheScanActuallyFindsTheCircleRoutes()
    {
        var guarded = CircleRoutes().Count(NamesACircle);

        Assert.True(guarded >= 14, $"Only {guarded} circle routes were found; the scan is not reaching them.");
    }

    private static bool NamesACircle(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.Parameters.Any(parameter =>
            string.Equals(parameter.Name, CircleRoleFilter.CircleIdRouteParameter, StringComparison.Ordinal));

    private static string NameOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName
        ?? endpoint.DisplayName
        ?? endpoint.RoutePattern.RawText
        ?? "(unnamed)";

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join('/', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}";

    private IEnumerable<RouteEndpoint> CircleRoutes() =>
        Endpoints().Where(endpoint =>
            (endpoint.RoutePattern.RawText ?? string.Empty)
            .Contains("/circles", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every mapped endpoint. Resolved as the <em>collection</em> of data sources: the host composes
    /// several, and asking for a single <c>EndpointDataSource</c> would either fail or quietly return
    /// one of them — which would make every scan above pass by seeing less.
    /// </summary>
    private IEnumerable<RouteEndpoint> Endpoints() =>
        factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>();
}
