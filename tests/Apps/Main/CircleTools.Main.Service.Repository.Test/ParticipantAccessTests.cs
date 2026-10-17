using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// A participant of a shared-by-link group sees only their own pairing and edits only their own entry
/// (ADR-0080, FEAT-024, TP-024): the redacted join read, the participant key, the own-entry write that
/// survives a stale organizer push, leaving, the joined-groups list and the taken-members list.
/// </summary>
[Collection(SharedGroupDatabase.CollectionName)]
public sealed class ParticipantAccessTests(SharedGroupPostgresFixture fixture)
{
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Carla = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Dora = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Christmas = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid DoraAccount = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // A group memento shaped like the Secret-Santa client's (camel-cased GroupState): four members with gift
    // notes, Dora linked to an account; one event with an earlier and a current draw, a carried-forward
    // baseline and everyone's wishlist; legacy single-exchange draws.
    private static string GroupJson(Guid groupId) => $$"""
        {
          "id": "{{groupId}}",
          "name": "Family",
          "members": [
            { "id": "{{Anna}}", "name": "Anna", "giftNote": "no nuts" },
            { "id": "{{Ben}}", "name": "Ben", "giftNote": "vegan" },
            { "id": "{{Carla}}", "name": "Carla", "giftNote": "no perfume" },
            { "id": "{{Dora}}", "name": "Dora", "giftNote": "books", "userId": "{{DoraAccount}}" }
          ],
          "exclusions": [ { "giverMemberId": "{{Anna}}", "recipientMemberId": "{{Ben}}" } ],
          "draws": [ { "id": "{{Guid.NewGuid()}}", "createdAt": "2025-12-01T00:00:00+00:00",
                       "assignments": [ { "giverMemberId": "{{Ben}}", "recipientMemberId": "{{Anna}}" } ] } ],
          "events": [
            {
              "id": "{{Christmas}}",
              "name": "Christmas",
              "draws": [
                { "id": "{{Guid.NewGuid()}}", "createdAt": "2026-11-01T00:00:00+00:00",
                  "assignments": [
                    { "giverMemberId": "{{Ben}}", "recipientMemberId": "{{Dora}}" },
                    { "giverMemberId": "{{Anna}}", "recipientMemberId": "{{Carla}}" } ] },
                { "id": "{{Guid.NewGuid()}}", "createdAt": "2026-11-02T00:00:00+00:00",
                  "assignments": [
                    { "giverMemberId": "{{Anna}}", "recipientMemberId": "{{Dora}}" },
                    { "giverMemberId": "{{Ben}}", "recipientMemberId": "{{Carla}}" },
                    { "giverMemberId": "{{Carla}}", "recipientMemberId": "{{Anna}}" },
                    { "giverMemberId": "{{Dora}}", "recipientMemberId": "{{Ben}}" } ] }
              ],
              "drawInvalidatedByMembershipChange": false,
              "carriedForwardBaseline": { "id": "{{Guid.NewGuid()}}", "createdAt": "2025-12-01T00:00:00+00:00",
                "assignments": [ { "giverMemberId": "{{Ben}}", "recipientMemberId": "{{Anna}}" } ] },
              "wishlists": [
                { "memberId": "{{Anna}}", "items": [ "scarf" ] },
                { "memberId": "{{Ben}}", "items": [ "book" ] },
                { "memberId": "{{Carla}}", "items": [ "tea" ] },
                { "memberId": "{{Dora}}", "items": [ "socks" ] }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task JoinRead_WithTheParticipantKey_HoldsOnlyTheMembersOwnPairing()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);

        var view = await Store(provider).GetAsync(links.JoinToken, new ParticipantProof(key, null));

        Assert.True(view!.Redacted);
        Assert.Equal(Ben, view.ClaimedMemberId);
        var occasion = view.Body.GetProperty("events")[0];
        var draws = occasion.GetProperty("draws");
        Assert.Equal(1, draws.GetArrayLength()); // only the current draw, no history
        var assignments = draws[0].GetProperty("assignments");
        Assert.Equal(1, assignments.GetArrayLength());
        Assert.Equal(Ben, assignments[0].GetProperty("giverMemberId").GetGuid());
        Assert.Equal(Carla, assignments[0].GetProperty("recipientMemberId").GetGuid());
        Assert.False(occasion.TryGetProperty("carriedForwardBaseline", out _));
        Assert.Equal(0, view.Body.GetProperty("draws").GetArrayLength()); // legacy draws gone

        // Only Ben's and his recipient Carla's wishlist and gift note remain.
        var wishlistOwners = occasion.GetProperty("wishlists").EnumerateArray()
            .Select(w => w.GetProperty("memberId").GetGuid()).ToHashSet();
        Assert.Equal([Ben, Carla], wishlistOwners.Order());
        Assert.Equal(["vegan", "no perfume"], GiftNotes(view.Body));
        Assert.False(Member(view.Body, Dora).TryGetProperty("userId", out _)); // other accounts not exposed
    }

    [Fact]
    public async Task JoinRead_WithoutProof_HoldsNoPairingNoWishlistNoGiftNote()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);

        var view = await Store(provider).GetAsync(links.JoinToken, ParticipantProof.None);

        Assert.Null(view!.ClaimedMemberId);
        var occasion = view.Body.GetProperty("events")[0];
        Assert.Equal(1, occasion.GetProperty("draws").GetArrayLength()); // still reads as drawn
        Assert.Equal(0, occasion.GetProperty("draws")[0].GetProperty("assignments").GetArrayLength());
        Assert.Equal(0, occasion.GetProperty("wishlists").GetArrayLength());
        Assert.Empty(GiftNotes(view.Body));
        Assert.Equal(4, view.Body.GetProperty("members").GetArrayLength()); // the roster stays
    }

    [Fact]
    public async Task JoinRead_WithAWrongKey_ProvesNothing()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await ClaimAsDevice(provider, links.JoinToken, Ben);

        var view = await Store(provider).GetAsync(links.JoinToken, new ParticipantProof("not-the-key", null));

        Assert.Null(view!.ClaimedMemberId);
        Assert.Equal(0, view.Body.GetProperty("events")[0].GetProperty("draws")[0].GetProperty("assignments").GetArrayLength());
    }

    [Fact]
    public async Task AdminRead_StaysFull()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);

        var view = await Store(provider).GetAsync(links.AdminToken, ParticipantProof.None);

        Assert.False(view!.Redacted);
        Assert.Equal(2, view.Body.GetProperty("events")[0].GetProperty("draws").GetArrayLength());
        Assert.Equal(4, GiftNotes(view.Body).Count);
    }

    [Fact]
    public async Task AccountClaim_IsProvenByTheAccount_OnAnyDevice_AndListedAsJoined()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var account = Guid.NewGuid();

        var claim = await Store(provider).ClaimPersonAsync(
            links.JoinToken, new ClaimPersonRequest(Ben, PersonClaimKind.Account, "device-a", Guid.NewGuid()), account);
        Assert.Equal(PersonClaimStatus.Claimed, claim.Status);

        // Another device of the account has no key, only the account.
        var view = await Store(provider).GetAsync(links.JoinToken, new ParticipantProof(null, account));
        Assert.Equal(Ben, view!.ClaimedMemberId);

        var joined = await Store(provider).ListJoinedAsync(account);
        var entry = Assert.Single(joined);
        Assert.Equal(groupId, entry.GroupId);
        Assert.Equal(Ben, entry.MemberId);
        Assert.Equal(links.JoinToken, entry.JoinToken);
    }

    [Fact]
    public async Task AccountClaim_WithoutASignedInCaller_IsRememberedAsADeviceClaim()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        var namedAccount = Guid.NewGuid();

        // The body names an account, but nobody signed in: the server never trusts the named id.
        await Store(provider).ClaimPersonAsync(
            links.JoinToken, new ClaimPersonRequest(Ben, PersonClaimKind.Account, "device-a", namedAccount), null);

        Assert.Empty(await Store(provider).ListJoinedAsync(namedAccount));
        var view = await Store(provider).GetAsync(links.JoinToken, new ParticipantProof(null, namedAccount));
        Assert.Null(view!.ClaimedMemberId);
    }

    [Fact]
    public async Task LegacyClaim_ExchangesItsDeviceHandleForAKey_Once()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        await ClaimAsDevice(provider, links.JoinToken, Ben, "handle-1");
        await ForgetKeyAsync(provider, groupId, Ben); // as if claimed before keys existed

        Assert.Null(await Store(provider).IssueParticipantKeyAsync(links.JoinToken, Ben, "someone-else"));
        var key = await Store(provider).IssueParticipantKeyAsync(links.JoinToken, Ben, "handle-1");
        Assert.NotNull(key);
        Assert.Null(await Store(provider).IssueParticipantKeyAsync(links.JoinToken, Ben, "handle-1")); // once only

        var view = await Store(provider).GetAsync(links.JoinToken, new ParticipantProof(key, null));
        Assert.Equal(Ben, view!.ClaimedMemberId);
    }

    [Fact]
    public async Task WriteEntry_PatchesTheBody_AndBumpsTheVersion()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);

        var result = await Store(provider).WriteEntryAsync(
            links.JoinToken,
            new ParticipantProof(key, null),
            new ParticipantEntry("gluten-free", [new ParticipantWishlist(Christmas, ["puzzle", "  ", "chess"])]));

        Assert.Equal(SharedWriteStatus.Applied, result.Status);
        Assert.Equal(2, result.Version);
        var admin = await Store(provider).GetAsync(links.AdminToken, ParticipantProof.None);
        Assert.Equal(2, admin!.Version);
        Assert.Equal("gluten-free", Member(admin.Body, Ben).GetProperty("giftNote").GetString());
        Assert.Equal(["puzzle", "chess"], WishlistOf(admin.Body, Ben));
        Assert.Equal(["scarf"], WishlistOf(admin.Body, Anna)); // others untouched
    }

    [Fact]
    public async Task WriteEntry_WithoutAProvenClaim_IsForbidden_AndOverTheLimits_TooLarge()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);

        var unproven = await Store(provider).WriteEntryAsync(
            links.JoinToken, ParticipantProof.None, new ParticipantEntry("x", []));
        var tooLong = await Store(provider).WriteEntryAsync(
            links.JoinToken, new ParticipantProof(key, null), new ParticipantEntry(new string('x', 501), []));
        var byAdminToken = await Store(provider).WriteEntryAsync(
            links.AdminToken, new ParticipantProof(key, null), new ParticipantEntry("x", []));

        Assert.Equal(SharedWriteStatus.Forbidden, unproven.Status);
        Assert.Equal(SharedWriteStatus.TooLarge, tooLong.Status);
        Assert.Equal(SharedWriteStatus.NotFound, byAdminToken.Status);
    }

    [Fact]
    public async Task AStaleOrganizerPush_DoesNotUndoTheParticipantsEntry_AndSaysRewritten()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);
        await Store(provider).WriteEntryAsync(
            links.JoinToken, new ParticipantProof(key, null), new ParticipantEntry("vegan, no soy", [new ParticipantWishlist(Christmas, ["kite"])]));

        // The organizer pushes its older copy (Ben's old entry) with a renamed group.
        var stale = GroupJson(groupId).Replace("\"Family\"", "\"Family 2026\"", StringComparison.Ordinal);
        var push = await Store(provider).PushAsync(links.AdminToken, 3, Body(stale));

        Assert.True(push.Rewritten);
        var admin = await Store(provider).GetAsync(links.AdminToken, ParticipantProof.None);
        Assert.Equal("Family 2026", admin!.Body.GetProperty("name").GetString()); // the organizer's change lands
        Assert.Equal("vegan, no soy", Member(admin.Body, Ben).GetProperty("giftNote").GetString()); // Ben's stays
        Assert.Equal(["kite"], WishlistOf(admin.Body, Ben));
    }

    [Fact]
    public async Task Leaving_ReleasesTheClaim_AndDropsTheEntry()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);
        await Store(provider).WriteEntryAsync(
            links.JoinToken, new ParticipantProof(key, null), new ParticipantEntry("vegan, no soy", []));

        Assert.False(await Store(provider).LeaveAsync(links.JoinToken, ParticipantProof.None));
        Assert.True(await Store(provider).LeaveAsync(links.JoinToken, new ParticipantProof(key, null)));

        Assert.DoesNotContain(Ben, (await Store(provider).GetClaimedMembersAsync(links.JoinToken))!);
        // The entry is gone: an organizer push is no longer rewritten.
        var push = await Store(provider).PushAsync(links.AdminToken, 5, Body(GroupJson(groupId)));
        Assert.False(push.Rewritten);
    }

    [Fact]
    public async Task ReleaseByTheAdmin_DropsTheEntryToo()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);
        await Store(provider).WriteEntryAsync(links.JoinToken, new ParticipantProof(key, null), new ParticipantEntry("x", []));

        Assert.True(await Store(provider).ReleasePersonAsync(links.AdminToken, Ben));

        var push = await Store(provider).PushAsync(links.AdminToken, 5, Body(GroupJson(groupId)));
        Assert.False(push.Rewritten);
    }

    [Fact]
    public async Task TakenMembers_NameTheOrganizer_LinkedAndJoinedMembers()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await ClaimAsDevice(provider, links.JoinToken, Ben);

        var before = await Store(provider).GetAsync(links.JoinToken, ParticipantProof.None);
        Assert.Contains(new TakenMember(Dora, TakenReason.Linked), before!.TakenMembers!);
        Assert.Contains(new TakenMember(Ben, TakenReason.Joined), before.TakenMembers!);
        Assert.DoesNotContain(before.TakenMembers!, t => t.MemberId == Anna);

        // Once Dora's account owns the group, she is its organizer.
        Assert.True(await Store(provider).ClaimAdminAsync(links.AdminToken, DoraAccount, "Dora"));
        var after = await Store(provider).GetAsync(links.JoinToken, ParticipantProof.None);
        Assert.Contains(new TakenMember(Dora, TakenReason.Organizer), after!.TakenMembers!);
    }

    [Fact]
    public async Task ReleasingAnAccountsClaims_RemovesClaimsAndEntries()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var account = Guid.NewGuid();
        await Store(provider).ClaimPersonAsync(links.JoinToken, new ClaimPersonRequest(Ben, PersonClaimKind.Account, null, null), account);
        await Store(provider).WriteEntryAsync(links.JoinToken, new ParticipantProof(null, account), new ParticipantEntry("x", []));

        Assert.Equal(1, await Store(provider).ReleaseClaimsOfAsync(account));

        Assert.Empty(await Store(provider).ListJoinedAsync(account));
        Assert.False((await Store(provider).PushAsync(links.AdminToken, 5, Body(GroupJson(groupId)))).Rewritten);
    }

    [Fact]
    public async Task TheOwner_ReleasesAJoinedPerson_AndNobodyElseCan()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        var key = await ClaimAsDevice(provider, links.JoinToken, Ben);
        await Store(provider).WriteEntryAsync(links.JoinToken, new ParticipantProof(key, null), new ParticipantEntry("x", []));
        var owner = Guid.NewGuid();
        Assert.True(await Store(provider).ClaimAdminAsync(links.AdminToken, owner, "Owner"));

        Assert.False(await Store(provider).ReleasePersonAsOwnerAsync(Guid.NewGuid(), groupId, Ben));
        Assert.Contains(Ben, (await Store(provider).GetClaimedMembersAsync(links.JoinToken))!);

        Assert.True(await Store(provider).ReleasePersonAsOwnerAsync(owner, groupId, Ben));
        Assert.DoesNotContain(Ben, (await Store(provider).GetClaimedMembersAsync(links.JoinToken))!);
        Assert.False((await Store(provider).PushAsOwnerAsync(owner, groupId, 9, Body(GroupJson(groupId)))).Rewritten);
    }

    [Fact]
    public async Task OrganizerFacts_ReportOwnerAndAdminToken()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);

        var withAdmin = await Store(provider).GetOrganizerAsync(groupId, links.AdminToken);
        var withJoin = await Store(provider).GetOrganizerAsync(groupId, links.JoinToken);
        var otherGroup = await Store(provider).GetOrganizerAsync(Guid.NewGuid(), links.AdminToken);

        Assert.True(withAdmin!.AdminTokenValid);
        Assert.Null(withAdmin.OwnerAccountId);
        Assert.False(withJoin!.AdminTokenValid);
        Assert.Null(otherGroup);
    }

    private static async Task<(MintedLinks Links, Guid GroupId)> ShareGroup(ServiceProvider provider)
    {
        var groupId = Guid.NewGuid();
        var result = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body(GroupJson(groupId)), "creator-key"));
        Assert.Equal(CreateSharedGroupStatus.Created, result.Status);
        return (result.Links!, groupId);
    }

    private static async Task<string> ClaimAsDevice(ServiceProvider provider, string joinToken, Guid member, string handle = "device-a")
    {
        var claim = await Store(provider).ClaimPersonAsync(
            joinToken, new ClaimPersonRequest(member, PersonClaimKind.Device, handle, null), null);
        Assert.Equal(PersonClaimStatus.Claimed, claim.Status);
        Assert.False(string.IsNullOrEmpty(claim.ParticipantKey));
        return claim.ParticipantKey!;
    }

    private static async Task ForgetKeyAsync(ServiceProvider provider, Guid groupId, Guid member)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SharedGroupDbContext>();
        var claim = await context.MemberClaims.FindAsync(groupId, member);
        claim!.ParticipantKeyHash = null;
        await context.SaveChangesAsync();
    }

    private static List<string> GiftNotes(JsonElement body) =>
        [.. body.GetProperty("members").EnumerateArray()
            .Where(m => m.TryGetProperty("giftNote", out _))
            .Select(m => m.GetProperty("giftNote").GetString()!)];

    private static JsonElement Member(JsonElement body, Guid id) =>
        body.GetProperty("members").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id);

    private static List<string> WishlistOf(JsonElement body, Guid member) =>
        [.. body.GetProperty("events")[0].GetProperty("wishlists").EnumerateArray()
            .Single(w => w.GetProperty("memberId").GetGuid() == member)
            .GetProperty("items").EnumerateArray().Select(i => i.GetString()!)];

    private static ISharedGroupStore Store(ServiceProvider provider) =>
        provider.CreateScope().ServiceProvider.GetRequiredService<ISharedGroupStore>();

    private static JsonElement Body(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
