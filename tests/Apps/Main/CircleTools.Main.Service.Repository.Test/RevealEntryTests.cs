using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// The reveal-link holder keeps their own gift note and wishlist (ADR-0084, FEAT-026, TP-026): a reveal token
/// reads and writes exactly its own member's entry for its own event — stored like a joined participant's,
/// re-applied over stale organizer pushes, listed as self-maintained, dropped on release — and nothing else.
/// </summary>
[Collection(SharedGroupDatabase.CollectionName)]
public sealed class RevealEntryTests(SharedGroupPostgresFixture fixture)
{
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Dora = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Christmas = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Easter = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DoraAccount = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // The fixture's database is shared by every test, and reveal slices are keyed by their token: each test
    // instance gets its own tokens.
    private readonly string _run = Guid.NewGuid().ToString("N");

    private string T(string token) => $"{token}-{_run}";

    // Three members (Dora linked to an account), two events, each with a current draw and wishlists.
    private static string GroupJson(Guid groupId) => $$"""
        {
          "id": "{{groupId}}",
          "name": "Family",
          "members": [
            { "id": "{{Anna}}", "name": "Anna", "giftNote": "no nuts" },
            { "id": "{{Ben}}", "name": "Ben", "giftNote": "vegan" },
            { "id": "{{Dora}}", "name": "Dora", "userId": "{{DoraAccount}}" }
          ],
          "events": [
            {
              "id": "{{Christmas}}",
              "name": "Christmas",
              "draws": [ { "id": "{{Guid.NewGuid()}}", "createdAt": "2026-11-02T00:00:00+00:00",
                  "assignments": [
                    { "giverMemberId": "{{Anna}}", "recipientMemberId": "{{Ben}}" },
                    { "giverMemberId": "{{Ben}}", "recipientMemberId": "{{Dora}}" },
                    { "giverMemberId": "{{Dora}}", "recipientMemberId": "{{Anna}}" } ] } ],
              "wishlists": [
                { "memberId": "{{Anna}}", "items": [ "scarf" ] },
                { "memberId": "{{Ben}}", "items": [ "book" ] }
              ]
            },
            {
              "id": "{{Easter}}",
              "name": "Easter",
              "draws": [],
              "wishlists": [ { "memberId": "{{Ben}}", "items": [ "chocolate" ] } ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Read_GivesTheHoldersGiftNote_AndTheirWishlistForTheTokensEvent()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-ben"), Christmas, Ben));

        var entry = await Store(provider).GetRevealEntryAsync(T("rt-ben"));

        Assert.Equal("vegan", entry!.GiftNote);
        Assert.Equal(["book"], entry.Items);
    }

    [Fact]
    public async Task Write_UpdatesTheBody_BumpsTheVersion_AndTouchesNothingElse()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-ben"), Christmas, Ben));

        var result = await Store(provider).WriteRevealEntryAsync(T("rt-ben"), new RevealEntryWrite("vegan, no soy", ["kite", "  ", " puzzle "]));

        Assert.Equal(SharedWriteStatus.Applied, result.Status);
        Assert.Equal(2, result.Version);
        var admin = await Store(provider).GetAsync(links.AdminToken, ParticipantProof.None);
        Assert.Equal("vegan, no soy", Member(admin!.Body, Ben).GetProperty("giftNote").GetString());
        Assert.Equal(["kite", "puzzle"], WishlistOf(admin.Body, Christmas, Ben));
        Assert.Equal(["chocolate"], WishlistOf(admin.Body, Easter, Ben)); // another event: untouched
        Assert.Equal(["scarf"], WishlistOf(admin.Body, Christmas, Anna)); // another member: untouched
        Assert.Equal("no nuts", Member(admin.Body, Anna).GetProperty("giftNote").GetString());
        Assert.Equal(["kite", "puzzle"], (await Store(provider).GetRevealEntryAsync(T("rt-ben")))!.Items);
    }

    [Fact]
    public async Task Write_KeepsTheMembersOtherEventsInTheirEntry()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-ben-xmas"), Christmas, Ben), (T("rt-ben-easter"), Easter, Ben));

        await Store(provider).WriteRevealEntryAsync(T("rt-ben-easter"), new RevealEntryWrite("vegan", ["bunny"]));
        await Store(provider).WriteRevealEntryAsync(T("rt-ben-xmas"), new RevealEntryWrite("vegan", ["kite"]));

        // A stale organizer push re-applies both events from the entry.
        var push = await Store(provider).PushAsync(links.AdminToken, 9, Body(GroupJson(groupId)));
        Assert.True(push.Rewritten);
        var admin = await Store(provider).GetAsync(links.AdminToken, ParticipantProof.None);
        Assert.Equal(["kite"], WishlistOf(admin!.Body, Christmas, Ben));
        Assert.Equal(["bunny"], WishlistOf(admin.Body, Easter, Ben));
    }

    [Fact]
    public async Task TheHolder_IsListedAsSelfMaintained_UntilTheAdminReleasesThem()
    {
        await using var provider = fixture.CreateProvider();
        var (links, groupId) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-ben"), Christmas, Ben));
        Assert.Empty(await Store(provider).GetSelfMaintainedMembersAsync(links.JoinToken));

        await Store(provider).WriteRevealEntryAsync(T("rt-ben"), new RevealEntryWrite(null, ["kite"]));

        Assert.Equal([Ben], await Store(provider).GetSelfMaintainedMembersAsync(links.AdminToken));
        Assert.Equal([Ben], await Store(provider).GetSelfMaintainedMembersAsync(links.JoinToken));
        Assert.Empty((await Store(provider).GetClaimedMembersAsync(links.AdminToken))!); // no claim was made

        Assert.True(await Store(provider).ReleasePersonAsync(links.AdminToken, Ben));
        Assert.Empty(await Store(provider).GetSelfMaintainedMembersAsync(links.AdminToken));
        var push = await Store(provider).PushAsync(links.AdminToken, 9, Body(GroupJson(groupId)));
        Assert.False(push.Rewritten);
    }

    [Fact]
    public async Task ALinkedMember_GetsNoEditor()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-dora"), Christmas, Dora));

        Assert.Null(await Store(provider).GetRevealEntryAsync(T("rt-dora")));
        var write = await Store(provider).WriteRevealEntryAsync(T("rt-dora"), new RevealEntryWrite(null, ["x"]));
        Assert.Equal(SharedWriteStatus.NotFound, write.Status);
    }

    [Fact]
    public async Task ASliceWithoutMemberOrEvent_AndAnUnknownToken_GetNoEditor()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await Store(provider).SetRevealSlicesAsync(links.AdminToken, [new RevealSlice(T("rt-old"), Body("{}"), "old-key")]);

        Assert.Null(await Store(provider).GetRevealEntryAsync(T("rt-old")));
        Assert.Null(await Store(provider).GetRevealEntryAsync(T("rt-unknown")));
        Assert.Equal(SharedWriteStatus.NotFound, (await Store(provider).WriteRevealEntryAsync(T("rt-old"), new RevealEntryWrite(null, []))).Status);
        Assert.Equal(SharedWriteStatus.NotFound, (await Store(provider).WriteRevealEntryAsync("", new RevealEntryWrite(null, []))).Status);
    }

    [Fact]
    public async Task AnOversizedEntry_IsTooLarge_AndNotStored()
    {
        await using var provider = fixture.CreateProvider();
        var (links, _) = await ShareGroup(provider);
        await PublishAsync(provider, links.AdminToken, (T("rt-ben"), Christmas, Ben));

        var longNote = await Store(provider).WriteRevealEntryAsync(T("rt-ben"), new RevealEntryWrite(new string('x', 501), []));
        var tooMany = await Store(provider).WriteRevealEntryAsync(T("rt-ben"), new RevealEntryWrite(null, [.. Enumerable.Repeat("x", 101)]));

        Assert.Equal(SharedWriteStatus.TooLarge, longNote.Status);
        Assert.Equal(SharedWriteStatus.TooLarge, tooMany.Status);
        Assert.Empty(await Store(provider).GetSelfMaintainedMembersAsync(links.AdminToken));
        Assert.Equal("vegan", (await Store(provider).GetRevealEntryAsync(T("rt-ben")))!.GiftNote);
    }

    private static async Task PublishAsync(ServiceProvider provider, string adminToken, params (string Token, Guid EventId, Guid MemberId)[] slices)
    {
        var published = await Store(provider).SetRevealSlicesAsync(
            adminToken,
            [.. slices.Select(s => new RevealSlice(s.Token, Body("{}"), $"{s.EventId:N}:{s.MemberId:N}", s.EventId, s.MemberId))]);
        Assert.NotNull(published);
    }

    private static async Task<(MintedLinks Links, Guid GroupId)> ShareGroup(ServiceProvider provider)
    {
        var groupId = Guid.NewGuid();
        var result = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body(GroupJson(groupId)), "creator-key"));
        Assert.Equal(CreateSharedGroupStatus.Created, result.Status);
        return (result.Links!, groupId);
    }

    private static JsonElement Member(JsonElement body, Guid id) =>
        body.GetProperty("members").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id);

    private static List<string> WishlistOf(JsonElement body, Guid eventId, Guid member) =>
        [.. body.GetProperty("events").EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == eventId)
            .GetProperty("wishlists").EnumerateArray()
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
