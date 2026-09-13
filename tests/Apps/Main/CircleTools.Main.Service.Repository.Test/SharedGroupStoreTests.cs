using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Behaviour of <see cref="SharedGroupStore"/> against real Postgres (FEAT-013, TP-013): token
/// capability, the no-oracle resolution of bad tokens, last-write-wins on push, admin-link rotation,
/// owner-less delete, and the NFR-009 abuse caps. The group body is asserted to survive as an opaque
/// document — the store never binds the Secret-Santa schema.
/// </summary>
[Collection(SharedGroupDatabase.CollectionName)]
public sealed class SharedGroupStoreTests(SharedGroupPostgresFixture fixture)
{
    // Each store call runs in its own DI scope — a fresh DbContext per operation, exactly as one
    // request per operation does in the host. Sharing one tracking context across a bulk ExecuteDelete
    // and a later read is a test artifact, not how the store is used.

    [Fact]
    public async Task Create_MintsTwoDistinctTokens_ResolvingToTheRightCapabilities()
    {
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();

        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body("""{ "name": "Family" }""")));

        Assert.NotNull(links);
        Assert.NotEqual(links!.AdminToken, links.JoinToken);

        var admin = await Store(provider).GetAsync(links.AdminToken);
        var join = await Store(provider).GetAsync(links.JoinToken);
        Assert.Equal(SharedCapability.Admin, admin!.Capability);
        Assert.Equal(SharedCapability.Join, join!.Capability);
        Assert.Equal("Family", admin.Body.GetProperty("name").GetString());
        Assert.False(admin.AdminRetired);
    }

    [Fact]
    public async Task Get_WithAWrongToken_IsAnIndistinguishableNotFound()
    {
        await using var provider = fixture.CreateProvider();

        Assert.Null(await Store(provider).GetAsync("not-a-real-token"));
        Assert.Null(await Store(provider).GetAsync(""));
    }

    [Fact]
    public async Task Push_WithAdminToken_FastForwards_AndIsReadableByTheJoinToken()
    {
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body("""{ "name": "Family" }""")));

        var result = await Store(provider).PushAsync(links!.AdminToken, 2, Body("""{ "name": "Family 2026" }"""));

        Assert.Equal(SharedWriteStatus.Applied, result.Status);
        Assert.Equal(2, result.Version);
        var join = await Store(provider).GetAsync(links.JoinToken);
        Assert.Equal("Family 2026", join!.Body.GetProperty("name").GetString());
        Assert.Equal(2, join.Version);
    }

    [Fact]
    public async Task Push_WithJoinToken_IsForbidden()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{}""")));

        var result = await Store(provider).PushAsync(links!.JoinToken, 2, Body("""{ "hacked": true }"""));

        Assert.Equal(SharedWriteStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Push_AtOrBelowStoredVersion_IsACollision_AndTheIncomingBodyWins()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 5, Body("""{ "v": "stored" }""")));

        // A concurrent editor pushing an equal/older version arrives later by the server clock and wins
        // in full — the collision is reported, not hidden.
        var result = await Store(provider).PushAsync(links!.AdminToken, 5, Body("""{ "v": "incoming" }"""));

        Assert.Equal(SharedWriteStatus.Collision, result.Status);
        var view = await Store(provider).GetAsync(links.AdminToken);
        Assert.Equal("incoming", view!.Body.GetProperty("v").GetString());
    }

    [Fact]
    public async Task RotateAdmin_RetiresTheOldAdminLink_AndTheNewOneWorks()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{}""")));

        var rotated = await Store(provider).RotateAdminAsync(links!.AdminToken);

        Assert.NotNull(rotated);
        Assert.NotEqual(links.AdminToken, rotated!.AdminToken);
        Assert.Null(await Store(provider).GetAsync(links.AdminToken)); // old admin link no longer resolves
        Assert.NotNull(await Store(provider).GetAsync(rotated.AdminToken));
        Assert.NotNull(await Store(provider).GetAsync(links.JoinToken)); // join link unaffected
        Assert.Equal(SharedWriteStatus.Applied, (await Store(provider).PushAsync(rotated.AdminToken, 2, Body("""{}"""))).Status);
    }

    [Fact]
    public async Task RotateAdmin_WithAJoinToken_IsRefused()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{}""")));

        Assert.Null(await Store(provider).RotateAdminAsync(links!.JoinToken));
    }

    [Fact]
    public async Task Delete_WithAdminToken_RemovesTheGroupEverywhere()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{}""")));

        Assert.True(await Store(provider).DeleteAsync(links!.AdminToken));
        Assert.Null(await Store(provider).GetAsync(links.JoinToken));
        Assert.Null(await Store(provider).GetAsync(links.AdminToken));
    }

    [Fact]
    public async Task Delete_WithJoinToken_IsRefused()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{}""")));

        Assert.False(await Store(provider).DeleteAsync(links!.JoinToken));
        Assert.NotNull(await Store(provider).GetAsync(links.JoinToken));
    }

    [Fact]
    public async Task Create_ForAGroupAlreadyShared_ReturnsTheSameLinks()
    {
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();
        var first = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body("""{}""")));

        var second = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, 1, Body("""{}""")));

        Assert.Equal(first!.AdminToken, second!.AdminToken);
        Assert.Equal(first.JoinToken, second.JoinToken);
    }

    [Fact]
    public async Task Create_WithABodyOverTheByteCap_IsRejected()
    {
        await using var provider = fixture.CreateProvider(limits => limits.MaxBodyBytes = 512);
        var big = "{ \"blob\": \"" + new string('x', 2000) + "\" }";

        Assert.Null(await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body(big))));
    }

    [Fact]
    public async Task Push_WithTooManyMembers_IsRejectedAsTooLarge()
    {
        await using var provider = fixture.CreateProvider(limits => limits.MaxMembers = 3);
        var links = await Store(provider).CreateAsync(new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("""{ "members": [] }""")));

        var members = string.Join(",", Enumerable.Range(0, 5).Select(i => $"{{ \"name\": \"m{i}\" }}"));
        var result = await Store(provider).PushAsync(links!.AdminToken, 2, Body($$"""{ "members": [{{members}}] }"""));

        Assert.Equal(SharedWriteStatus.TooLarge, result.Status);
    }

    private static ISharedGroupStore Store(ServiceProvider provider) =>
        provider.CreateScope().ServiceProvider.GetRequiredService<ISharedGroupStore>();

    private static JsonElement Body(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

/// <summary>Entropy and uniqueness of the share-token factory (NFR-009) — no database needed.</summary>
public sealed class RandomShareTokenFactoryTests
{
    [Fact]
    public void Create_ProducesDistinct_UrlSafe_HighEntropyTokens()
    {
        var factory = new RandomShareTokenFactory();

        var tokens = Enumerable.Range(0, 1000).Select(_ => factory.Create()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count()); // all distinct
        Assert.All(tokens, t =>
        {
            Assert.True(t.Length >= 40); // 32 bytes base64url ≈ 43 chars → well past 128-bit
            Assert.DoesNotContain('+', t);
            Assert.DoesNotContain('/', t);
            Assert.DoesNotContain('=', t);
        });
    }
}
