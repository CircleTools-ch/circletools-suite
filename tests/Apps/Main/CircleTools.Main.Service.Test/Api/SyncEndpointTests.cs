using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Exercises <c>POST /api/v1/sync</c> through the whole stack — routing, model binding, JWT
/// validation and the real Postgres <c>sync</c> schema (FEAT-004). The store's own rules
/// (sequences, last-write-wins, retention) are covered against Postgres in
/// <c>CircleTools.Main.Service.Repository.Test</c>; this asserts the route is wired, bearer-authorized
/// and account-scoped, and that a no-op cycle carries no bodies.
/// </summary>
[Collection(AccountApi.CollectionName)]
public sealed class SyncEndpointTests(AccountApiFactory factory)
{
    [Fact]
    public async Task Sync_WithoutABearerToken_Is401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sync", EmptyCycle("device-x"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnAuthenticatedCycle_AppliesAChange_AndEchoesItAboveTheCursor()
    {
        var client = await SignedInClientAsync();
        var groupId = Guid.NewGuid();

        var request = new SyncRequest(
            "device-a",
            Cursor: 0,
            Changes: [new SyncChange("group", groupId, 1, Body("""{ "name": "Family" }"""))],
            Tombstones: []);

        var response = await client.PostAsJsonAsync("/api/v1/sync", request);
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<SyncResponse>())!;
        Assert.Equal(1, body.Cursor);
        var change = Assert.Single(body.Changes);
        Assert.Equal(groupId, change.Id);
        Assert.Equal("Family", change.Body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ANoChangeCycle_ReturnsNoBodies()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/sync", EmptyCycle("device-b"));
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<SyncResponse>())!;
        Assert.Empty(body.Changes);
        Assert.Empty(body.Tombstones);
        Assert.Empty(body.Unclaimed);
        Assert.Empty(body.Collisions);
    }

    [Fact]
    public async Task OneAccountsData_IsNotVisibleToAnother()
    {
        var groupId = Guid.NewGuid();
        var first = await SignedInClientAsync();
        await first.PostAsJsonAsync(
            "/api/v1/sync",
            new SyncRequest("device-1", 0, [new SyncChange("group", groupId, 1, Body("""{ "name": "Mine" }"""))], []));

        var second = await SignedInClientAsync();
        var response = await second.PostAsJsonAsync("/api/v1/sync", EmptyCycle("device-2"));
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<SyncResponse>())!;
        Assert.DoesNotContain(body.Changes, c => c.Id == groupId);
    }

    [Fact]
    public async Task Claim_WithoutABearerToken_Is401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/sync/groups/{Guid.NewGuid()}/claim",
            new ClaimGroupRequest(1, Body("""{ "name": "Family" }""")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claim_UploadsTheGroup_AndACatchingUpDevicePullsIt()
    {
        var client = await SignedInClientAsync();
        var groupId = Guid.NewGuid();

        var claim = await client.PostAsJsonAsync(
            $"/api/v1/sync/groups/{groupId}/claim",
            new ClaimGroupRequest(1, Body("""{ "name": "Claimed" }""")));
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);

        // The claimed group is now account-scoped and rides the normal cycle: a fresh device pulls it.
        var pull = await client.PostAsJsonAsync("/api/v1/sync", EmptyCycle("device-b"));
        pull.EnsureSuccessStatusCode();
        var body = (await pull.Content.ReadFromJsonAsync<SyncResponse>())!;
        var seen = body.Changes.Single(c => c.Id == groupId);
        Assert.Equal("Claimed", seen.Body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unclaim_WithoutABearerToken_Is401()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/sync/groups/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unclaim_RemovesTheServerCopy_AndPropagatesAnUnclaimMarker()
    {
        var client = await SignedInClientAsync();
        var groupId = Guid.NewGuid();
        await client.PostAsJsonAsync(
            "/api/v1/sync",
            new SyncRequest("device-a", 0, [new SyncChange("group", groupId, 1, Body("""{ "name": "Leaving" }"""))], []));

        var removal = await client.DeleteAsync($"/api/v1/sync/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);

        // A device catching up pulls the un-claim marker and no group body — the server copy is gone.
        var pull = await client.PostAsJsonAsync("/api/v1/sync", EmptyCycle("device-b"));
        pull.EnsureSuccessStatusCode();
        var body = (await pull.Content.ReadFromJsonAsync<SyncResponse>())!;
        Assert.DoesNotContain(body.Changes, c => c.Id == groupId);
        Assert.Contains(body.Unclaimed, u => u.Id == groupId);
    }

    private static SyncRequest EmptyCycle(string deviceId) => new(deviceId, 0, [], []);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private async Task<HttpClient> SignedInClientAsync()
    {
        // Sync only requires an authenticated session, not a verified address, so the account need
        // not go through email confirmation here.
        var email = $"sync-{Guid.CreateVersion7():N}@example.test";
        const string password = "correct-horse-battery";
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Sync Tester", email, password, "de-CH"));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return client;
    }
}
