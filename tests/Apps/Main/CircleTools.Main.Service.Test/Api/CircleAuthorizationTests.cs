// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Circles.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Covers the authorization properties a circle route must hold whoever calls it (NFR-006).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class CircleAuthorizationTests(MailApiFactory factory)
{
    /// <summary>
    /// The single most load-bearing property of the whole milestone: from outside a circle, "you are
    /// not in it" and "there is no such circle" are the same answer. Anything else turns every
    /// circle id into a probe for whether it exists.
    /// </summary>
    [Fact]
    public async Task NonMemberAndUnknownCircle_AnswerIdentically()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");

        var toARealCircle = await bea.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));
        var toNothing = await bea.Client.GetAsync(new Uri($"/api/v1/circles/{Guid.CreateVersion7()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, toARealCircle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, toNothing.StatusCode);
        Assert.Equal(await Body(toNothing), await Body(toARealCircle));
    }

    /// <summary>
    /// Reads a failure body without the <c>traceId</c> <c>ProblemDetails</c> stamps on it. That
    /// value differs on every request whatever the outcome, so leaving it in would compare noise and
    /// leaving the comparison at "both are 404" would miss a body that says which 404 it was.
    /// </summary>
    private static async Task<string> Body(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return JsonSerializer.Serialize(document.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.Ordinal))
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.Ordinal));
    }

    /// <summary>
    /// A member is not an admin. The refusal has to come from the server, because hiding the button
    /// is not an access control.
    /// </summary>
    [Fact]
    public async Task Member_CallingAnAdminRouteDirectly_IsRefused()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var response = await bea.Client.GetAsync(
            new Uri($"/api/v1/circles/{circle.Id}/invitations", UriKind.Relative));

        // Forbidden, not Not-Found: a member already knows the circle exists, so 403 reveals nothing
        // they could not see anyway.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// FR-020's "the effect is immediate", which is a claim about tokens as much as about roles: no
    /// role ever enters an access token, so a removal takes hold on the very next request rather than
    /// at the next refresh. The token here is deliberately the one issued <em>before</em> the removal.
    /// </summary>
    [Fact]
    public async Task RemovedMember_IsRefusedOnTheNextRequest_WithTheSameAccessToken()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var whileAMember = await bea.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, whileAMember.StatusCode);

        var people = (await anna.Client.GetFromJsonAsync<CircleDetailResponse>(
            $"/api/v1/circles/{circle.Id}"))!.People;

        var removed = await anna.Client.DeleteAsync(new Uri(
            $"/api/v1/circles/{circle.Id}/memberships/{people.Single(p => p.DisplayName == "Bea").MembershipId}",
            UriKind.Relative));

        removed.EnsureSuccessStatusCode();

        // Same client, same bearer token, no sign-out in between.
        var afterRemoval = await bea.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));

        // Not-Found rather than Forbidden: Bea is now a stranger to this circle, and a stranger is
        // told nothing about whether it exists.
        Assert.Equal(HttpStatusCode.NotFound, afterRemoval.StatusCode);

        var mine = await bea.Client.GetFromJsonAsync<IReadOnlyList<CircleSummaryResponse>>("/api/v1/circles");
        Assert.DoesNotContain(circle.Id, mine!.Select(c => c.Id));
    }

    /// <summary>
    /// The other direction of the same property: a demotion narrows what the existing session may do
    /// immediately, and does not need the user to sign out to take effect.
    /// </summary>
    [Fact]
    public async Task DemotedAdmin_IsAuthorizedUnderTheNewRoleWithoutSigningOut()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var people = (await anna.Client.GetFromJsonAsync<CircleDetailResponse>(
            $"/api/v1/circles/{circle.Id}"))!.People;

        var membershipId = people.Single(p => p.DisplayName == "Bea").MembershipId;
        var pending = new Uri($"/api/v1/circles/{circle.Id}/invitations", UriKind.Relative);

        await Role(anna, circle.Id, membershipId, "admin");
        Assert.Equal(HttpStatusCode.OK, (await bea.Client.GetAsync(pending)).StatusCode);

        await Role(anna, circle.Id, membershipId, "member");

        // Still a member, so still 403 rather than 404 — and refused on the admin route at once.
        var asAMember = await bea.Client.GetAsync(pending);
        Assert.Equal(HttpStatusCode.Forbidden, asAMember.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await bea.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative))).StatusCode);
    }

    private static async Task Role(CircleTestAccount owner, Guid circleId, Guid membershipId, string role)
    {
        var response = await owner.Client.PutAsJsonAsync(
            $"/api/v1/circles/{circleId}/memberships/{membershipId}/role",
            new ChangeRoleRequest(role));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Anonymous_IsRefusedOnEveryCircleRoute()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
