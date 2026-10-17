using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// ADR-0080 over real HTTP (FEAT-024, TP-024): only the organizer may publish a group's linking projection
/// — a participant or stranger gets 403, never a 500 from a key collision — and a participant proves their
/// claimed member with the participant-key header or their bearer on the anonymous shared-group routes.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class ParticipantAccessEndpointTests(MailApiFactory factory)
{
    private const string ParticipantKeyHeader = "X-Participant-Key";
    private const string AdminTokenHeader = "X-Admin-Token";

    [Fact]
    public async Task ALocalGroupsFirstPublisherOrganizesIt_ASecondAccountIs403_NotA500()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var other = await CircleTestAccounts.SignUpAsync(factory, "Other");
        var groupId = Guid.NewGuid();

        await LinkingScenario.RegisterDefaultEventAsync(organizer, groupId);

        var events = await other.Client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/events",
            new PublishGroupEventsRequest("Hijacked", [new GroupEventEntry(groupId, "x", null)]));
        var assignments = await other.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/assignments/publish",
            new PublishAssignmentsRequest(groupId, "Hijacked", groupId, "x", 99, null, null, []));

        Assert.Equal(HttpStatusCode.Forbidden, events.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assignments.StatusCode);

        // The organizer keeps publishing.
        await LinkingScenario.RegisterDefaultEventAsync(organizer, groupId, "December 2026");
    }

    [Fact]
    public async Task ASharedGroup_IsPublishedByItsAdminTokenHolder_NotByAParticipant_EvenIfTheParticipantComesFirst()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var participant = await CircleTestAccounts.SignUpAsync(factory, "Participant");
        var groupId = Guid.NewGuid();
        var links = await ShareAsync(groupId);

        // The participant's device publishes first — refused, because the server holds the group and the
        // participant neither owns it nor holds its admin token.
        var early = await participant.Client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/events",
            new PublishGroupEventsRequest("Family", [new GroupEventEntry(groupId, "Christmas", null)]));
        Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/groups/{groupId}/events")
        {
            Content = JsonContent.Create(new PublishGroupEventsRequest("Family", [new GroupEventEntry(groupId, "Christmas", null)])),
        };
        request.Headers.Add(AdminTokenHeader, links.AdminToken);
        var byAdmin = await organizer.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, byAdmin.StatusCode);
    }

    [Fact]
    public async Task AParticipantProvesTheirMember_WithTheKeyHeader_OrTheirBearer()
    {
        var participant = await CircleTestAccounts.SignUpAsync(factory, "Participant");
        var groupId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var links = await ShareAsync(groupId, member);

        // Claim through the signed-in client: an account claim, bound to the bearer.
        var claim = await participant.Client.PostAsJsonAsync(
            $"/api/v1/shared/{links.JoinToken}/claims",
            new ClaimPersonRequest(member, PersonClaimKind.Account, "device-a", null));
        var claimed = (await claim.Content.ReadFromJsonAsync<PersonClaimResponse>())!;
        Assert.True(claimed.Claimed);
        Assert.False(string.IsNullOrEmpty(claimed.ParticipantKey));

        // Signed-out device with the key.
        using var anonymous = factory.CreateClient();
        using var keyed = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/shared/{links.JoinToken}");
        keyed.Headers.Add(ParticipantKeyHeader, claimed.ParticipantKey);
        var byKey = (await (await anonymous.SendAsync(keyed)).Content.ReadFromJsonAsync<SharedGroupView>())!;
        Assert.Equal(member, byKey.ClaimedMemberId);
        Assert.True(byKey.Redacted);

        // Another device of the account: the bearer alone.
        var byBearer = (await participant.Client.GetFromJsonAsync<SharedGroupView>($"/api/v1/shared/{links.JoinToken}"))!;
        Assert.Equal(member, byBearer.ClaimedMemberId);

        // Nothing presented: no member.
        var unproven = (await anonymous.GetFromJsonAsync<SharedGroupView>($"/api/v1/shared/{links.JoinToken}"))!;
        Assert.Null(unproven.ClaimedMemberId);

        // The account's other devices find the group.
        var joined = (await participant.Client.GetFromJsonAsync<JoinedSharedGroups>("/api/v1/shared/joined"))!;
        Assert.Contains(joined.Groups, g => g.GroupId == groupId && g.MemberId == member && g.JoinToken == links.JoinToken);
    }

    [Fact]
    public async Task AParticipantWritesTheirOwnEntry_AndLeaves_OnlyWithProof()
    {
        var groupId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var links = await ShareAsync(groupId, member);
        using var device = factory.CreateClient();

        var claim = await device.PostAsJsonAsync(
            $"/api/v1/shared/{links.JoinToken}/claims",
            new ClaimPersonRequest(member, PersonClaimKind.Device, "device-a", null));
        var key = (await claim.Content.ReadFromJsonAsync<PersonClaimResponse>())!.ParticipantKey!;
        var entry = new ParticipantEntry("vegan", [new ParticipantWishlist(groupId, ["book"])]);

        var unproven = await device.PutAsJsonAsync($"/api/v1/shared/{links.JoinToken}/me", entry);
        Assert.Equal(HttpStatusCode.Forbidden, unproven.StatusCode);

        using var write = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/shared/{links.JoinToken}/me")
        {
            Content = JsonContent.Create(entry),
        };
        write.Headers.Add(ParticipantKeyHeader, key);
        var written = await device.SendAsync(write);
        Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        Assert.Equal(2, (await written.Content.ReadFromJsonAsync<ParticipantEntryResult>())!.Version);

        using var leave = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/shared/{links.JoinToken}/claims/me");
        leave.Headers.Add(ParticipantKeyHeader, key);
        Assert.Equal(HttpStatusCode.NoContent, (await device.SendAsync(leave)).StatusCode);

        var claims = (await device.GetFromJsonAsync<ClaimedMembers>($"/api/v1/shared/{links.JoinToken}/claims"))!;
        Assert.DoesNotContain(member, claims.MemberIds);
    }

    private async Task<MintedLinks> ShareAsync(Guid groupId, Guid? member = null)
    {
        var memberId = member ?? Guid.NewGuid();
        using var document = JsonDocument.Parse($$"""
            {
              "id": "{{groupId}}", "name": "Family",
              "members": [ { "id": "{{memberId}}", "name": "Ben" } ],
              "exclusions": [], "draws": [],
              "events": [ { "id": "{{groupId}}", "name": "Christmas", "draws": [], "drawInvalidatedByMembershipChange": false, "wishlists": [] } ]
            }
            """);
        using var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync(
            "/api/v1/shared", new CreateSharedGroupRequest(groupId, 1, document.RootElement.Clone(), "creator-" + groupId));
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<MintedLinks>())!;
    }
}
