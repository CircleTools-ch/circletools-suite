using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The account-free create endpoint's creator-key gate over real HTTP (FEAT-013, ADR-0058 security
/// note). The route is anonymous and the group id is disclosed to every join-link holder, so a re-create
/// must return the links only to the caller that re-presents the matching creator key — never to one who
/// knows just the id. Verifies the 200 / 403 status mapping the store's <c>Refused</c> outcome drives.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class SharedGroupEndpointTests(MailApiFactory factory)
{
    private static JsonElement Body()
    {
        using var document = JsonDocument.Parse("""{ "name": "Family" }""");
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Create_ThenResumeWithTheMatchingKey_ReturnsTheSameLinks_ButAWrongKeyIs403()
    {
        using var client = factory.CreateClient();
        var groupId = Guid.NewGuid();

        // First share: created, links returned.
        var first = await client.PostAsJsonAsync(
            "/api/v1/shared", new CreateSharedGroupRequest(groupId, 1, Body(), "the-creators-secret"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var links = await first.Content.ReadFromJsonAsync<MintedLinks>();

        // A caller who knows the group id but not the creator key (a join-link holder) is refused, with
        // no links disclosed — the join-vs-admin boundary holds.
        var wrong = await client.PostAsJsonAsync(
            "/api/v1/shared", new CreateSharedGroupRequest(groupId, 1, Body(), "a-guessed-key"));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

        // The original creator's lost-response retry (matching key) gets the same links back.
        var resume = await client.PostAsJsonAsync(
            "/api/v1/shared", new CreateSharedGroupRequest(groupId, 1, Body(), "the-creators-secret"));
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        var resumed = await resume.Content.ReadFromJsonAsync<MintedLinks>();
        Assert.Equal(links!.AdminToken, resumed!.AdminToken);
        Assert.Equal(links.JoinToken, resumed.JoinToken);
    }
}
