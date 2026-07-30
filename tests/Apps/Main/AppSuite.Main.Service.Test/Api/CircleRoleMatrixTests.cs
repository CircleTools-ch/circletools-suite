using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppSuite.Circles.Contracts;
using AppSuite.Circles.Core.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// NFR-006's role matrix: every circle-scoped route × owner / admin / member / non-member /
/// anonymous, asserting the permitted <em>and</em> the refused outcome.
/// </summary>
/// <remarks>
/// <para>
/// The failure mode this exists for is invisible to every functional test: one endpoint that forgot a
/// check, called only ever as an owner. So the table below is driven per route rather than per
/// scenario, and <see cref="TheMatrix_CoversEveryCircleScopedRoute"/> ties it to the endpoint data
/// source, so a route added later is missing from the matrix loudly rather than quietly.
/// </para>
/// <para>
/// Every refused row is a <strong>direct API call</strong>. That is deliberate: NFR-006's "hiding the
/// button is not the control" cannot be demonstrated by a component test, only by calling the route
/// the hidden button would have called.
/// </para>
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class CircleRoleMatrixTests(MailApiFactory factory)
{
    /// <summary>
    /// Every route that names a circle, with the least authority that may call it.
    /// </summary>
    /// <remarks>
    /// The request each row builds is a <em>plausible</em> one: real membership and invitation
    /// identifiers from the circle under test, so a refusal can only have come from the caller's role
    /// and never from a malformed request. The three in-app invitation routes are not here — they are
    /// reached by people who are not in the circle yet, and <c>CircleEndpointGuardTests</c> records
    /// why they carry no role requirement.
    /// </remarks>
    private static readonly CircleRouteCase[] _routes =
    [
        new("GET /circles/{id}", CircleRole.Member, s =>
            Request(HttpMethod.Get, $"/api/v1/circles/{s.CircleId}")),
        new("PUT /circles/{id}", CircleRole.Admin, s =>
            Request(HttpMethod.Put, $"/api/v1/circles/{s.CircleId}", new UpdateCircleRequest("Müller", "Familie"))),
        new("DELETE /circles/{id}", CircleRole.Owner, s =>
            Request(HttpMethod.Delete, $"/api/v1/circles/{s.CircleId}", new DeleteCircleRequest(s.Name))),
        new("POST /circles/{id}/invitations", CircleRole.Admin, s =>
            Request(
                HttpMethod.Post,
                $"/api/v1/circles/{s.CircleId}/invitations",
                new InviteRequest($"matrix-{Guid.CreateVersion7():N}@example.test", ManagedMembershipId: null))),
        new("GET /circles/{id}/invitations", CircleRole.Admin, s =>
            Request(HttpMethod.Get, $"/api/v1/circles/{s.CircleId}/invitations")),
        new("POST /circles/{id}/invitations/{iid}/resend", CircleRole.Admin, s =>
            Request(HttpMethod.Post, $"/api/v1/circles/{s.CircleId}/invitations/{s.PendingInvitationId}/resend")),
        new("DELETE /circles/{id}/invitations/{iid}", CircleRole.Admin, s =>
            Request(HttpMethod.Delete, $"/api/v1/circles/{s.CircleId}/invitations/{s.PendingInvitationId}")),
        new("POST /circles/{id}/managed-members", CircleRole.Admin, s =>
            Request(
                HttpMethod.Post,
                $"/api/v1/circles/{s.CircleId}/managed-members",
                new AddManagedMemberRequest("Grosi", "de-CH"))),
        new("PUT /circles/{id}/managed-members/{mid}", CircleRole.Admin, s =>
            Request(
                HttpMethod.Put,
                $"/api/v1/circles/{s.CircleId}/managed-members/{s.ManagedMembershipId}",
                new UpdateManagedMemberRequest("Grossmutter", "de-CH"))),
        new("DELETE /circles/{id}/managed-members/{mid}", CircleRole.Admin, s =>
            Request(HttpMethod.Delete, $"/api/v1/circles/{s.CircleId}/managed-members/{s.ManagedMembershipId}")),
        new("PUT /circles/{id}/memberships/{mid}/role", CircleRole.Owner, s =>
            Request(
                HttpMethod.Put,
                $"/api/v1/circles/{s.CircleId}/memberships/{s.MemberMembershipId}/role",
                new ChangeRoleRequest("admin"))),
        new("POST /circles/{id}/memberships/{mid}/transfer-ownership", CircleRole.Owner, s =>
            Request(
                HttpMethod.Post,
                $"/api/v1/circles/{s.CircleId}/memberships/{s.MemberMembershipId}/transfer-ownership")),
        new("DELETE /circles/{id}/memberships/{mid}", CircleRole.Admin, s =>
            Request(HttpMethod.Delete, $"/api/v1/circles/{s.CircleId}/memberships/{s.MemberMembershipId}")),
        new("POST /circles/{id}/leave", CircleRole.Member, s =>
            Request(HttpMethod.Post, $"/api/v1/circles/{s.CircleId}/leave")),
    ];

    /// <summary>
    /// The cast, built once. Four accounts and their verified addresses are expensive to create and
    /// carry no per-test state; the <em>circle</em> is what each row gets fresh.
    /// </summary>
    private static readonly SemaphoreSlim _castGate = new(1, 1);
    private static Cast? _cast;
    private static Subject? _sharedSubject;

    /// <summary>Rows where the caller holds enough authority for the route.</summary>
    /// <returns>Route name × caller kind.</returns>
    public static TheoryData<string, string> Permitted() => Rows(sufficient: true);

    /// <summary>Rows where the caller is in the circle but not high enough in it.</summary>
    /// <returns>Route name × caller kind.</returns>
    public static TheoryData<string, string> Insufficient() => Rows(sufficient: false);

    /// <summary>Every route in the matrix, once.</summary>
    /// <returns>Route names.</returns>
    public static TheoryData<string> EveryRoute()
    {
        var data = new TheoryData<string>();

        foreach (var route in _routes)
        {
            data.Add(route.Name);
        }

        return data;
    }

    /// <summary>
    /// A caller who holds the role reaches the handler. The assertion is deliberately <em>not</em>
    /// "succeeds": an owner calling <c>leave</c> is authorized and still refused, because a circle
    /// may not be left without an owner (FR-021). Authorization and the domain's answer are different
    /// questions, and conflating them here would have forced the matrix to encode FR-021's rules a
    /// second time. What must never happen is an authorization refusal — <c>401</c>, <c>403</c> or the
    /// <c>404</c> a stranger gets.
    /// </summary>
    /// <param name="routeName">The route under test.</param>
    /// <param name="callerKind">Which member of the cast is calling.</param>
    /// <returns>A task that completes when the row has been asserted.</returns>
    [Theory]
    [MemberData(nameof(Permitted))]
    public async Task ASufficientRole_ReachesTheHandler(string routeName, string callerKind)
    {
        var route = Route(routeName);
        var cast = await CastAsync();

        // A fresh circle per row: almost every permitted call mutates the circle — deleting it,
        // transferring it, removing the member the next row targets — so a shared one would make the
        // matrix order-dependent, which is the one thing a matrix must not be.
        var subject = await BuildSubjectAsync(cast);

        var response = await Caller(cast, callerKind).SendAsync(route.Build(subject));

        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict,
            $"{callerKind} holds {route.Least} or better on {routeName} but was answered {(int)response.StatusCode}.");
    }

    /// <summary>
    /// A member is not an admin and an admin is not the owner, and the server says so — whatever the
    /// client chose to show.
    /// </summary>
    /// <param name="routeName">The route under test.</param>
    /// <param name="callerKind">Which member of the cast is calling.</param>
    /// <returns>A task that completes when the row has been asserted.</returns>
    [Theory]
    [MemberData(nameof(Insufficient))]
    public async Task AnInsufficientRole_IsRefused(string routeName, string callerKind)
    {
        var route = Route(routeName);
        var cast = await CastAsync();
        var subject = await SharedSubjectAsync(cast);

        var response = await Caller(cast, callerKind).SendAsync(route.Build(subject));

        // Forbidden rather than Not-Found: a member already knows the circle exists, so 403 tells
        // them nothing they could not see anyway. For a non-member the answer must be 404 — that is
        // the row below.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The milestone's most load-bearing property, on <strong>every</strong> route rather than only on
    /// the one that is easy to check: from outside a circle, "you are not in it" and "there is no such
    /// circle" are the same answer, body included. Anything else turns every route into a probe for
    /// whether a circle exists.
    /// </summary>
    /// <param name="routeName">The route under test.</param>
    /// <returns>A task that completes when the row has been asserted.</returns>
    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task ANonMember_IsAnsweredExactlyAsForACircleThatDoesNotExist(string routeName)
    {
        var route = Route(routeName);
        var cast = await CastAsync();
        var subject = await SharedSubjectAsync(cast);

        var toARealCircle = await cast.Outsider.Client.SendAsync(route.Build(subject));
        var toNothing = await cast.Outsider.Client.SendAsync(
            route.Build(subject with { CircleId = Guid.CreateVersion7() }));

        Assert.Equal(HttpStatusCode.NotFound, toARealCircle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, toNothing.StatusCode);
        Assert.Equal(await Body(toNothing), await Body(toARealCircle));
    }

    /// <summary>No circle route serves a caller without a session.</summary>
    /// <param name="routeName">The route under test.</param>
    /// <returns>A task that completes when the row has been asserted.</returns>
    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task AnAnonymousCaller_IsRefused(string routeName)
    {
        var route = Route(routeName);
        var cast = await CastAsync();
        var subject = await SharedSubjectAsync(cast);

        var response = await factory.CreateClient().SendAsync(route.Build(subject));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ties the hand-written table to reality. Without this, the matrix would silently stop covering
    /// the endpoint somebody adds next year — which is exactly the failure NFR-006 asks to be ruled
    /// out structurally.
    /// </summary>
    [Fact]
    public void TheMatrix_CoversEveryCircleScopedRoute()
    {
        var mapped = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<CircleRoleRequirement>() is not null)
            .Select(endpoint =>
                $"{string.Join('/', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} /{endpoint.RoutePattern.RawText}")
            .ToList();

        Assert.Equal(_routes.Length, mapped.Count);
    }

    private static TheoryData<string, string> Rows(bool sufficient)
    {
        var data = new TheoryData<string, string>();

        foreach (var route in _routes)
        {
            foreach (var (kind, role) in _members)
            {
                if (role >= route.Least == sufficient)
                {
                    data.Add(route.Name, kind);
                }
            }
        }

        return data;
    }

    /// <summary>The three kinds of caller who are <em>in</em> the circle, and what they hold.</summary>
    private static readonly (string Kind, CircleRole Role)[] _members =
    [
        ("owner", CircleRole.Owner),
        ("admin", CircleRole.Admin),
        ("member", CircleRole.Member),
    ];

    private static CircleRouteCase Route(string name) =>
        _routes.Single(route => string.Equals(route.Name, name, StringComparison.Ordinal));

    private static HttpClient Caller(Cast cast, string kind) => kind switch
    {
        "owner" => cast.Owner.Client,
        "admin" => cast.Admin.Client,
        "member" => cast.Member.Client,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown caller kind."),
    };

    private static HttpRequestMessage Request(HttpMethod method, string url, object? body = null) =>
        new(method, new Uri(url, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType()),
        };

    /// <summary>
    /// Reads a failure body without the <c>traceId</c> <c>ProblemDetails</c> stamps on it — that
    /// value differs on every request whatever the outcome, so comparing it would compare noise.
    /// </summary>
    private static async Task<string> Body(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(raw);

        return JsonSerializer.Serialize(document.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.Ordinal))
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.Ordinal));
    }

    private async Task<Cast> CastAsync()
    {
        await _castGate.WaitAsync();

        try
        {
            return _cast ??= new Cast(
                await CircleTestAccounts.SignUpAsync(factory, "Anna"),
                await CircleTestAccounts.SignUpAsync(factory, "Bea"),
                await CircleTestAccounts.SignUpAsync(factory, "Cara"),
                await CircleTestAccounts.SignUpAsync(factory, "Dora"));
        }
        finally
        {
            _castGate.Release();
        }
    }

    /// <summary>
    /// One circle shared by every <em>refused</em> row. A refusal changes nothing by definition, so
    /// sharing it is safe here and only here.
    /// </summary>
    private async Task<Subject> SharedSubjectAsync(Cast cast)
    {
        await _castGate.WaitAsync();

        try
        {
            return _sharedSubject ??= await BuildSubjectAsync(cast);
        }
        finally
        {
            _castGate.Release();
        }
    }

    /// <summary>
    /// Builds a circle holding one of everything a route can name: an admin, a plain member, a
    /// managed member and a pending invitation.
    /// </summary>
    private async Task<Subject> BuildSubjectAsync(Cast cast)
    {
        const string Name = "Müller";

        var circle = await CircleFixture.CreateAsync(cast.Owner, Name, "Familie");

        await CircleFixture.AddMemberAsync(factory, cast.Owner, cast.Admin, circle.Id);
        await CircleFixture.AddMemberAsync(factory, cast.Owner, cast.Member, circle.Id);

        var people = (await cast.Owner.Client.GetFromJsonAsync<CircleDetailResponse>(
            $"/api/v1/circles/{circle.Id}"))!.People;

        var adminMembership = people.Single(p => p.DisplayName == cast.Admin.DisplayName).MembershipId;
        var memberMembership = people.Single(p => p.DisplayName == cast.Member.DisplayName).MembershipId;

        var promoted = await cast.Owner.Client.PutAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/memberships/{adminMembership}/role",
            new ChangeRoleRequest("admin"));

        promoted.EnsureSuccessStatusCode();

        var managed = await cast.Owner.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/managed-members",
            new AddManagedMemberRequest("Grosi", "de-CH"));

        managed.EnsureSuccessStatusCode();

        var invited = await cast.Owner.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/invitations",
            new InviteRequest($"pending-{Guid.CreateVersion7():N}@example.test", ManagedMembershipId: null));

        invited.EnsureSuccessStatusCode();

        var pending = await cast.Owner.Client.GetFromJsonAsync<IReadOnlyList<PendingInvitationResponse>>(
            $"/api/v1/circles/{circle.Id}/invitations");

        return new Subject(
            circle.Id,
            Name,
            adminMembership,
            memberMembership,
            (await managed.Content.ReadFromJsonAsync<CirclePersonResponse>())!.MembershipId,
            pending!.Single().Id);
    }

    /// <summary>A route, the least role that may call it, and a plausible request for it.</summary>
    /// <param name="Name">The route, as it appears in the test name.</param>
    /// <param name="Least">The least authority that may call it.</param>
    /// <param name="Build">Builds the request against a circle.</param>
    private sealed record CircleRouteCase(string Name, CircleRole Least, Func<Subject, HttpRequestMessage> Build);

    /// <summary>The four accounts every row is played by.</summary>
    private sealed record Cast(
        CircleTestAccount Owner,
        CircleTestAccount Admin,
        CircleTestAccount Member,
        CircleTestAccount Outsider);

    /// <summary>The circle a row is played against, and every identifier its routes can name.</summary>
    private sealed record Subject(
        Guid CircleId,
        string Name,
        Guid AdminMembershipId,
        Guid MemberMembershipId,
        Guid ManagedMembershipId,
        Guid PendingInvitationId);
}
