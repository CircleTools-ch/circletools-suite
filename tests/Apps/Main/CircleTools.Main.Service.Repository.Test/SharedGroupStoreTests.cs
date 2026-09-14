using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Behaviour of <see cref="SharedGroupStore"/> against real Postgres (FEAT-013, TP-013): token
/// capability, the no-oracle resolution of bad tokens, last-write-wins on push, admin-link rotation,
/// owner-less delete, the creator-key gate on re-share (ADR-0058 security note), and the NFR-009 abuse
/// caps. The group body is asserted to survive as an opaque document — the store never binds the
/// Secret-Santa schema.
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

        var links = await Share(provider, Guid.NewGuid(), """{ "name": "Family" }""");

        Assert.NotEqual(links.AdminToken, links.JoinToken);

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
        var links = await Share(provider, Guid.NewGuid(), """{ "name": "Family" }""");

        var result = await Store(provider).PushAsync(links.AdminToken, 2, Body("""{ "name": "Family 2026" }"""));

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
        var links = await Share(provider, Guid.NewGuid(), "{}");

        var result = await Store(provider).PushAsync(links.JoinToken, 2, Body("""{ "hacked": true }"""));

        Assert.Equal(SharedWriteStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Push_AtOrBelowStoredVersion_IsACollision_AndTheIncomingBodyWins()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), """{ "v": "stored" }""", version: 5);

        // A concurrent editor pushing an equal/older version arrives later by the server clock and wins
        // in full — the collision is reported, not hidden.
        var result = await Store(provider).PushAsync(links.AdminToken, 5, Body("""{ "v": "incoming" }"""));

        Assert.Equal(SharedWriteStatus.Collision, result.Status);
        var view = await Store(provider).GetAsync(links.AdminToken);
        Assert.Equal("incoming", view!.Body.GetProperty("v").GetString());
    }

    [Fact]
    public async Task RotateAdmin_RetiresTheOldAdminLink_AndTheNewOneWorks()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");

        var rotated = await Store(provider).RotateAdminAsync(links.AdminToken);

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
        var links = await Share(provider, Guid.NewGuid(), "{}");

        Assert.Null(await Store(provider).RotateAdminAsync(links.JoinToken));
    }

    [Fact]
    public async Task Delete_WithAdminToken_RemovesTheGroupEverywhere()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");

        Assert.True(await Store(provider).DeleteAsync(links.AdminToken));
        Assert.Null(await Store(provider).GetAsync(links.JoinToken));
        Assert.Null(await Store(provider).GetAsync(links.AdminToken));
    }

    [Fact]
    public async Task Delete_WithJoinToken_IsRefused()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");

        Assert.False(await Store(provider).DeleteAsync(links.JoinToken));
        Assert.NotNull(await Store(provider).GetAsync(links.JoinToken));
    }

    [Fact]
    public async Task Create_ResumeWithMatchingCreatorKey_ReturnsTheSameLinks()
    {
        // The lost-response retry: the creator re-posts the same group id and the same creator key, and
        // gets its existing links back (idempotent) rather than a duplicate row.
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();
        var first = await Share(provider, groupId, "{}", creatorKey: "the-creators-secret");

        var second = await Store(provider).CreateAsync(
            new CreateSharedGroupRequest(groupId, 1, Body("{}"), "the-creators-secret"));

        Assert.Equal(CreateSharedGroupStatus.Created, second.Status);
        Assert.Equal(first.AdminToken, second.Links!.AdminToken);
        Assert.Equal(first.JoinToken, second.Links.JoinToken);
    }

    [Fact]
    public async Task Create_ResumeWithAWrongCreatorKey_IsRefused_AndDisclosesNothing()
    {
        // The attack: someone who knows only the group id (every join-link holder learns it) re-posts it
        // with a different key hoping to be handed the admin token. They must be refused.
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();
        await Share(provider, groupId, "{}", creatorKey: "the-creators-secret");

        var attacker = await Store(provider).CreateAsync(
            new CreateSharedGroupRequest(groupId, 1, Body("{}"), "a-guessed-key"));

        Assert.Equal(CreateSharedGroupStatus.Refused, attacker.Status);
        Assert.Null(attacker.Links);
    }

    [Fact]
    public async Task Create_WithoutACreatorKey_IsRefused()
    {
        await using var provider = fixture.CreateProvider();

        var result = await Store(provider).CreateAsync(
            new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body("{}"), string.Empty));

        Assert.Equal(CreateSharedGroupStatus.Refused, result.Status);
    }

    [Fact]
    public async Task Create_ForAnAccountOwnedGroup_IsRefused_EvenWithTheCreatorKey()
    {
        // Once an account owns the group its admin link is retired; the anonymous create must never
        // re-mint one, even to the original creator — the owner manages it by bearer now.
        await using var provider = fixture.CreateProvider();
        var groupId = Guid.NewGuid();
        var links = await Share(provider, groupId, "{}", creatorKey: "the-creators-secret");
        await Store(provider).ClaimAdminAsync(links.AdminToken, Guid.NewGuid(), "Anna");

        var result = await Store(provider).CreateAsync(
            new CreateSharedGroupRequest(groupId, 1, Body("{}"), "the-creators-secret"));

        Assert.Equal(CreateSharedGroupStatus.Refused, result.Status);
    }

    [Fact]
    public async Task Create_WithABodyOverTheByteCap_IsRejectedAsTooLarge()
    {
        await using var provider = fixture.CreateProvider(limits => limits.MaxBodyBytes = 512);
        var big = "{ \"blob\": \"" + new string('x', 2000) + "\" }";

        var result = await Store(provider).CreateAsync(
            new CreateSharedGroupRequest(Guid.NewGuid(), 1, Body(big), "creator-key"));

        Assert.Equal(CreateSharedGroupStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task Push_WithTooManyMembers_IsRejectedAsTooLarge()
    {
        await using var provider = fixture.CreateProvider(limits => limits.MaxMembers = 3);
        var links = await Share(provider, Guid.NewGuid(), """{ "members": [] }""");

        var members = string.Join(",", Enumerable.Range(0, 5).Select(i => $"{{ \"name\": \"m{i}\" }}"));
        var result = await Store(provider).PushAsync(links.AdminToken, 2, Body($$"""{ "members": [{{members}}] }"""));

        Assert.Equal(SharedWriteStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task ClaimPerson_LocksTheMember_SoASecondClaimIsRefused()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");
        var member = Guid.NewGuid();

        var first = await Store(provider).ClaimPersonAsync(links.JoinToken,
            new ClaimPersonRequest(member, PersonClaimKind.Device, "device-a", null));
        var second = await Store(provider).ClaimPersonAsync(links.JoinToken,
            new ClaimPersonRequest(member, PersonClaimKind.Account, null, Guid.NewGuid()));

        Assert.Equal(PersonClaimStatus.Claimed, first.Status);
        Assert.Equal(PersonClaimStatus.AlreadyClaimed, second.Status);
        var claimed = await Store(provider).GetClaimedMembersAsync(links.JoinToken);
        Assert.Contains(member, claimed!);
    }

    [Fact]
    public async Task ReleasePerson_ByAdmin_FreesTheMemberToBeClaimedAgain()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");
        var member = Guid.NewGuid();
        await Store(provider).ClaimPersonAsync(links.JoinToken, new ClaimPersonRequest(member, PersonClaimKind.Device, "d", null));

        // A join token cannot release; an admin token can.
        Assert.False(await Store(provider).ReleasePersonAsync(links.JoinToken, member));
        Assert.True(await Store(provider).ReleasePersonAsync(links.AdminToken, member));

        // Freed — claimable again.
        var reclaim = await Store(provider).ClaimPersonAsync(links.JoinToken,
            new ClaimPersonRequest(member, PersonClaimKind.Device, "d2", null));
        Assert.Equal(PersonClaimStatus.Claimed, reclaim.Status);
    }

    [Fact]
    public async Task ClaimAdmin_RetiresTheAdminLink_ButTheJoinLinkKeepsWorking()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");
        var account = Guid.NewGuid();

        Assert.True(await Store(provider).ClaimAdminAsync(links.AdminToken, account, "Anna"));

        // The admin link now resolves to a "retired" view naming who took over; the join link is normal.
        var adminView = await Store(provider).GetAsync(links.AdminToken);
        Assert.True(adminView!.AdminRetired);
        Assert.Equal("Anna", adminView.AdminClaimedByName);
        var joinView = await Store(provider).GetAsync(links.JoinToken);
        Assert.False(joinView!.AdminRetired);

        // The retired admin token can no longer push.
        Assert.Equal(SharedWriteStatus.Forbidden, (await Store(provider).PushAsync(links.AdminToken, 2, Body("""{}"""))).Status);
    }

    [Fact]
    public async Task ReleaseAdmin_ByOwner_MintsAFreshAdminLink()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), "{}");
        var account = Guid.NewGuid();
        await Store(provider).ClaimAdminAsync(links.AdminToken, account, "Anna");

        Assert.Null(await Store(provider).ReleaseAdminAsync(Guid.NewGuid(), links.GroupId)); // not the owner
        var rotated = await Store(provider).ReleaseAdminAsync(account, links.GroupId);

        Assert.NotNull(rotated);
        var view = await Store(provider).GetAsync(rotated!.AdminToken);
        Assert.False(view!.AdminRetired);
        Assert.Equal(SharedWriteStatus.Applied, (await Store(provider).PushAsync(rotated.AdminToken, 2, Body("""{}"""))).Status);
    }

    [Fact]
    public async Task PruneExpired_RemovesOwnerLessInactiveGroups_ButKeepsOwnedOnes()
    {
        await using var provider = fixture.CreateProvider();
        var ownerLess = await Share(provider, Guid.NewGuid(), "{}");
        var owned = await Share(provider, Guid.NewGuid(), "{}");
        await Store(provider).ClaimAdminAsync(owned.AdminToken, Guid.NewGuid(), "Owner");

        var removed = await Store(provider).PruneExpiredAsync(DateTimeOffset.UtcNow.AddDays(1));

        Assert.True(removed >= 1);
        Assert.Null(await Store(provider).GetAsync(ownerLess.JoinToken)); // owner-less + inactive: swept
        Assert.NotNull(await Store(provider).GetAsync(owned.JoinToken)); // account-owned: kept
    }

    [Fact]
    public async Task ReleaseAllOwned_HandsOffAnAccountsGroups_LeavingJoinAccessIntact()
    {
        await using var provider = fixture.CreateProvider();
        var account = Guid.NewGuid();
        var owned = await Share(provider, Guid.NewGuid(), "{}");
        await Store(provider).ClaimAdminAsync(owned.AdminToken, account, "Anna");
        var otherOwned = await Share(provider, Guid.NewGuid(), "{}");
        await Store(provider).ClaimAdminAsync(otherOwned.AdminToken, Guid.NewGuid(), "Bruno");

        var handed = await Store(provider).ReleaseAllOwnedAsync(account);

        Assert.Equal(1, handed); // only the departing account's group
        Assert.NotNull(await Store(provider).GetAsync(owned.JoinToken)); // participants keep join access
        // The released group is now owner-less; the account can no longer release it as owner.
        Assert.Null(await Store(provider).ReleaseAdminAsync(account, owned.GroupId));
    }

    [Fact]
    public async Task DeleteAllOwned_RemovesAnAccountsGroupsEntirely_ButLeavesOthers()
    {
        await using var provider = fixture.CreateProvider();
        var account = Guid.NewGuid();
        var owned = await Share(provider, Guid.NewGuid(), "{}");
        await Store(provider).ClaimAdminAsync(owned.AdminToken, account, "Anna");
        // A member is claimed, to prove the delete takes the claims with it.
        await Store(provider).ClaimPersonAsync(owned.JoinToken, new ClaimPersonRequest(Guid.NewGuid(), PersonClaimKind.Device, "d", null));
        var otherOwned = await Share(provider, Guid.NewGuid(), "{}");
        await Store(provider).ClaimAdminAsync(otherOwned.AdminToken, Guid.NewGuid(), "Bruno");

        var deleted = await Store(provider).DeleteAllOwnedAsync(account);

        Assert.Equal(1, deleted); // only the departing account's group
        Assert.Null(await Store(provider).GetAsync(owned.JoinToken)); // the online copy is gone — no hand-off
        Assert.Null(await Store(provider).GetAsync(owned.AdminToken));
        Assert.NotNull(await Store(provider).GetAsync(otherOwned.JoinToken)); // another owner's group untouched
    }

    [Fact]
    public async Task Owner_CanReadAndWriteTheirClaimedGroup_ButOthersCannot()
    {
        await using var provider = fixture.CreateProvider();
        var links = await Share(provider, Guid.NewGuid(), """{ "v": "a" }""");
        var account = Guid.NewGuid();
        await Store(provider).ClaimAdminAsync(links.AdminToken, account, "Anna");

        // The owner reads and writes their claimed group by id + identity (no admin token).
        var view = await Store(provider).GetAsOwnerAsync(account, links.GroupId);
        Assert.NotNull(view);
        Assert.False(view!.AdminRetired);
        var push = await Store(provider).PushAsOwnerAsync(account, links.GroupId, view.Version + 1, Body("""{ "v": "b" }"""));
        Assert.Equal(SharedWriteStatus.Applied, push.Status);
        Assert.Equal("b", (await Store(provider).GetAsOwnerAsync(account, links.GroupId))!.Body.GetProperty("v").GetString());

        // A different account is not the owner.
        Assert.Null(await Store(provider).GetAsOwnerAsync(Guid.NewGuid(), links.GroupId));
        Assert.Equal(SharedWriteStatus.Forbidden,
            (await Store(provider).PushAsOwnerAsync(Guid.NewGuid(), links.GroupId, 99, Body("""{}"""))).Status);

        // The owner can discover their claimed groups from another device (D2 discovery).
        var owned = await Store(provider).ListOwnedAsync(account);
        Assert.Contains(owned, g => g.GroupId == links.GroupId);
        Assert.Empty(await Store(provider).ListOwnedAsync(Guid.NewGuid()));
    }

    // Shares a group and returns its links, asserting the create succeeded. The creator key defaults to a
    // fixed value; pass an explicit one to exercise the resume/refuse paths.
    private static async Task<MintedLinks> Share(
        ServiceProvider provider, Guid groupId, string json, long version = 1, string creatorKey = "creator-key-1")
    {
        var result = await Store(provider).CreateAsync(new CreateSharedGroupRequest(groupId, version, Body(json), creatorKey));
        Assert.Equal(CreateSharedGroupStatus.Created, result.Status);
        return result.Links!;
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
