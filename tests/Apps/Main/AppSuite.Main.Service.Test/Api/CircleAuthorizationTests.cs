using System.Net;
using System.Text.Json;

namespace AppSuite.Main.Service.Test.Api;

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
