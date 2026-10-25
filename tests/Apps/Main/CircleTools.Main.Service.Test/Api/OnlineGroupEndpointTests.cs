using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using static CircleTools.Main.Service.Test.Api.OnlineGroupScenario;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The Secret-Santa group store over real HTTP and Postgres (ADR-0086, TP-027). Privacy first: a participant gets
/// their own pairing and nothing else; a caller without a role gets the same 404 as a missing group.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class OnlineGroupEndpointTests(MailApiFactory factory)
{
    [Fact]
    public async Task TheOwner_ReadsAndPushes_AStrangerAndAParticipantCannotReadTheDocument()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var stranger = await CircleTestAccounts.SignUpAsync(factory, "Stranger");
        var (document, _, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);

        var read = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.Equal(1, read.Version);
        Assert.Contains("writeDocument", read.Permissions);
        Assert.Equal("owner", Assert.Single(read.Roles).Role);

        var renamed = Group.FromState(document);
        renamed.Rename("Family 2026");
        var push = await ReadAsync<PushOnlineGroupResponse>(await SendAsync(
            owner.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(read.Version, 1, Element(renamed.ToState()))));
        Assert.Equal(2, push.Version);
        Assert.False(push.Rewritten);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"{Base}/groups/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"{Base}/groups/{Guid.NewGuid()}")).StatusCode);

        var join = await ReadAsync<JoinResponse>(await SendAsync(
            stranger.Client, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Ben"], null, WithAccount: true)));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"{Base}/groups/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(
            stranger.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(2, 1, Element(document)))).StatusCode);
        Assert.Equal(members["Ben"], join.MemberId);
    }

    [Fact]
    public async Task AParticipantView_HoldsOnlyTheirOwnPairing()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, eventId, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        using var device = factory.CreateClient();

        var join = await ReadAsync<JoinResponse>(await SendAsync(
            device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Anna"], null, WithAccount: false)));
        Assert.False(string.IsNullOrEmpty(join.DeviceKey));

        var response = await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}/me", deviceKey: join.DeviceKey);
        var body = await response.Content.ReadAsStringAsync();
        var view = await ReadAsync<ParticipantView>(response);

        var recipient = RecipientOf(document, eventId, members["Anna"]);
        Assert.Equal(NameOf(document, recipient), Assert.Single(view.Events).Assignment!.RecipientName);
        foreach (var giver in members.Values.Where(m => m != members["Anna"]))
        {
            var other = RecipientOf(document, eventId, giver);
            if (other != recipient && other != members["Anna"])
            {
                Assert.DoesNotContain($"\"n\":\"{NameOf(document, other)}\"", body, StringComparison.Ordinal);
            }
        }

        // No key, a wrong key, or the document route: nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await device.GetAsync($"{Base}/groups/{document.Id}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}/me", deviceKey: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}", deviceKey: join.DeviceKey)).StatusCode);
    }

    [Fact]
    public async Task AMember_IsTakenOnce_AndAnAccountIsOneMember()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var ben = await CircleTestAccounts.SignUpAsync(factory, "Ben");
        var (document, _, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        using var device = factory.CreateClient();

        (await SendAsync(ben.Client, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Ben"], null, true))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(
            device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Ben"], null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(
            ben.Client, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Cleo"], null, true))).StatusCode);

        var preview = await ReadAsync<JoinPreview>(await device.GetAsync($"{Base}/join/{links.JoinToken}"));
        Assert.True(preview.Members.Single(m => m.Name == "Ben").Taken);
    }

    [Fact]
    public async Task JoiningUnderANewName_AddsTheMember()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, _, _) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        using var device = factory.CreateClient();

        var join = await ReadAsync<JoinResponse>(await SendAsync(
            device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(null, "Eve", false)));

        var read = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.Equal("Eve", Group.FromState(Read(read.Document)).FindMember(join.MemberId)!.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(
            device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(null, "Anna", false))).StatusCode);
    }

    [Fact]
    public async Task AnOwnEntry_ReachesTheOrganizer_AndSurvivesAnOlderPush_AndStaysAfterLeaving()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var (document, eventId, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        (await SendAsync(anna.Client, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Anna"], null, true))).EnsureSuccessStatusCode();

        var written = await ReadAsync<OwnEntryResponse>(await SendAsync(
            anna.Client, HttpMethod.Put, $"/groups/{document.Id}/me/entry",
            new OwnEntryRequest("No sweets", [new OwnWishlist(eventId, ["Book"])])));

        var read = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.Equal(written.Version, read.Version);
        Assert.Contains(members["Anna"], read.SelfMaintained);
        var merged = Group.FromState(Read(read.Document));
        Assert.Equal("No sweets", merged.FindMember(members["Anna"])!.GiftNote);
        Assert.Equal(["Book"], merged.FindEvent(eventId)!.WishlistFor(members["Anna"]).Items);

        // The organizer's device still holds the copy from before the entry: its push must not erase it.
        var push = await ReadAsync<PushOnlineGroupResponse>(await SendAsync(
            owner.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(1, 1, Element(document))));
        Assert.True(push.Rewritten);
        Assert.Equal(["Book"], Group.FromState(Read(push.Document)).FindEvent(eventId)!.WishlistFor(members["Anna"]).Items);

        (await anna.Client.DeleteAsync($"{Base}/groups/{document.Id}/me")).EnsureSuccessStatusCode();
        var after = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.DoesNotContain(members["Anna"], after.SelfMaintained);
        Assert.Equal("No sweets", Group.FromState(Read(after.Document)).FindMember(members["Anna"])!.GiftNote);
        Assert.Equal(HttpStatusCode.NotFound, (await anna.Client.GetAsync($"{Base}/groups/{document.Id}/me")).StatusCode);
    }

    [Fact]
    public async Task AnInvitation_IsAcceptedIntoTheSameParticipantView()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var cleo = await CircleTestAccounts.SignUpAsync(factory, "Cleo");
        var (document, _, members) = DrawnGroup();
        await CreateAsync(owner.Client, document, joinEnabled: false);

        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(
            owner.Client, HttpMethod.Post, $"/groups/{document.Id}/participants",
            new AddParticipantRequest(members["Cleo"], ParticipantRoute.Invitation, cleo.Email))).StatusCode);

        // An address without an account reads the same (NFR-005).
        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(
            owner.Client, HttpMethod.Post, $"/groups/{document.Id}/participants",
            new AddParticipantRequest(members["Dan"], ParticipantRoute.Invitation, "nobody-" + Guid.NewGuid() + "@example.test"))).StatusCode);

        var overview = await ReadAsync<MyGroups>(await cleo.Client.GetAsync($"{Base}/me/groups"));
        var invited = Assert.Single(overview.Groups, g => g.GroupId == document.Id);
        Assert.True(invited.Invited);
        Assert.Empty(invited.Permissions);
        Assert.Equal(HttpStatusCode.NotFound, (await cleo.Client.GetAsync($"{Base}/groups/{document.Id}/me")).StatusCode);

        (await cleo.Client.PostAsync($"{Base}/me/invitations/{document.Id}/accept", null)).EnsureSuccessStatusCode();
        var view = await ReadAsync<ParticipantView>(await cleo.Client.GetAsync($"{Base}/groups/{document.Id}/me"));
        Assert.Equal("Cleo", view.MemberName);
    }

    [Fact]
    public async Task TheOrganizersOwnMember_IsOneRowWithTheOwnerRole()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, _, members) = DrawnGroup();
        await CreateAsync(owner.Client, document);

        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(
            owner.Client, HttpMethod.Post, $"/groups/{document.Id}/participants",
            new AddParticipantRequest(members["Anna"], ParticipantRoute.OwnMember))).StatusCode);

        var overview = await ReadAsync<MyGroups>(await owner.Client.GetAsync($"{Base}/me/groups"));
        var mine = Assert.Single(overview.Groups, g => g.GroupId == document.Id);
        Assert.Equal("owner", mine.Role);
        Assert.Equal("Anna", mine.MemberName);

        var read = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.Equal(members["Anna"], Assert.Single(read.Roles).MemberId);
    }

    [Fact]
    public async Task ALinkOwnedGroup_IsOrganizedByItsLink_AndAnAccountCanTakeItOver()
    {
        var (document, _, _) = DrawnGroup();
        using var device = factory.CreateClient();
        var links = await CreateAsync(device, document, creatorKey: "creator-" + Guid.NewGuid());
        Assert.False(string.IsNullOrEmpty(links.OrganizerToken));

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}", organizerToken: links.OrganizerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await device.GetAsync($"{Base}/groups/{document.Id}")).StatusCode);

        var account = await CircleTestAccounts.SignUpAsync(factory, "Taker");
        (await SendAsync(account.Client, HttpMethod.Post, $"/groups/{document.Id}/take-over", organizerToken: links.OrganizerToken)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}", organizerToken: links.OrganizerToken)).StatusCode);
        var read = await ReadAsync<OrganizerGroupView>(await account.Client.GetAsync($"{Base}/groups/{document.Id}"));
        Assert.Equal("account", Assert.Single(read.Roles).Principal);

        // Handing it back mints a new organizer link.
        var handed = await ReadAsync<GroupLinks>(await account.Client.PostAsync($"{Base}/groups/{document.Id}/hand-over", null));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}", organizerToken: handed.OrganizerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await account.Client.GetAsync($"{Base}/groups/{document.Id}")).StatusCode);
    }

    [Fact]
    public async Task ARetriedCreate_RecoversTheLinks_OnlyForTheCreator()
    {
        var (document, _, _) = DrawnGroup();
        using var device = factory.CreateClient();
        var key = "creator-" + Guid.NewGuid();

        var first = await CreateAsync(device, document, creatorKey: key);
        var retry = await CreateAsync(device, document, creatorKey: key);
        Assert.Equal(first.OrganizerToken, retry.OrganizerToken);

        var other = await device.PostAsJsonAsync($"{Base}/groups", new CreateOnlineGroupRequest(1, Element(document), "someone-else", true));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
    }

    [Fact]
    public async Task ARevealPage_IsDerivedNow_AndItsHolderKeepsTheirEntry_UntilAParticipantDoes()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, eventId, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);

        var tokens = await ReadAsync<List<RevealTokenView>>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}/reveal-tokens"));
        Assert.Equal(4, tokens.Count);
        var again = await ReadAsync<List<RevealTokenView>>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}/reveal-tokens"));
        Assert.Equal(tokens.Select(t => t.Token).Order(), again.Select(t => t.Token).Order());

        using var anonymous = factory.CreateClient();
        var dan = tokens.Single(t => t.GiverMemberId == members["Dan"]);
        var page = await ReadAsync<RevealPayload>(await anonymous.GetAsync($"{Base}/reveal/{dan.Token}"));
        Assert.Equal(NameOf(document, RecipientOf(document, eventId, members["Dan"])), page.RecipientName);

        (await SendAsync(anonymous, HttpMethod.Put, $"/reveal/{dan.Token}/entry", new RevealEntry("Vegan", ["Plant"]))).EnsureSuccessStatusCode();
        var danGiver = tokens.Single(t => RecipientOf(document, eventId, t.GiverMemberId) == members["Dan"]);
        var giversPage = await ReadAsync<RevealPayload>(await anonymous.GetAsync($"{Base}/reveal/{danGiver.Token}"));
        Assert.Equal(["Plant"], giversPage.Wishlist);

        // Once Dan takes part, his reveal link no longer edits his entry: one owner per member.
        (await SendAsync(anonymous, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Dan"], null, false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{Base}/reveal/{dan.Token}/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{Base}/reveal/not-a-token")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentEntryWritesAndPushes_LoseNothing()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, eventId, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        var keys = new Dictionary<Guid, string>();
        foreach (var member in members.Values)
        {
            using var device = factory.CreateClient();
            var join = await ReadAsync<JoinResponse>(await SendAsync(device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(member, null, false)));
            keys[member] = join.DeviceKey!;
        }

        var writes = keys.Select(async pair =>
        {
            using var device = factory.CreateClient();
            var response = await SendAsync(device, HttpMethod.Put, $"/groups/{document.Id}/me/entry",
                new OwnEntryRequest(null, [new OwnWishlist(eventId, [$"wish-{pair.Key}"])]), deviceKey: pair.Value);
            response.EnsureSuccessStatusCode();
        }).Append(Task.Run(async () =>
        {
            var push = await SendAsync(owner.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(1, 1, Element(document)));
            push.EnsureSuccessStatusCode();
        }));
        await Task.WhenAll(writes);

        var read = await ReadAsync<OrganizerGroupView>(await owner.Client.GetAsync($"{Base}/groups/{document.Id}"));
        var merged = Group.FromState(Read(read.Document)).FindEvent(eventId)!;
        foreach (var member in members.Values)
        {
            Assert.Equal([$"wish-{member}"], merged.WishlistFor(member).Items);
        }
    }

    [Fact]
    public async Task ADocumentFromANewerSchema_IsRefused()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, _, _) = DrawnGroup();
        await CreateAsync(owner.Client, document);

        var push = await SendAsync(owner.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(1, 99, Element(document)));

        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
    }

    [Fact]
    public async Task DeletingTheGroup_EndsEveryoneElsesAccess()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (document, _, members) = DrawnGroup();
        var links = await CreateAsync(owner.Client, document);
        using var device = factory.CreateClient();
        var join = await ReadAsync<JoinResponse>(await SendAsync(device, HttpMethod.Post, $"/join/{links.JoinToken}", new JoinRequest(members["Ben"], null, false)));

        (await owner.Client.DeleteAsync($"{Base}/groups/{document.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(device, HttpMethod.Get, $"/groups/{document.Id}/me", deviceKey: join.DeviceKey)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await device.GetAsync($"{Base}/join/{links.JoinToken}")).StatusCode);
    }
}
