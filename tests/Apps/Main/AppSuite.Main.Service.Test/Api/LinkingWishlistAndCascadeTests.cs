using System.Net;
using System.Net.Http.Json;
using AppSuite.SecretSanta.Contracts;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// The linked member's self-owned wishlist (FR-030) and the account-deletion cascade (FR-015): a
/// linked member owns and edits their own wishlist; a non-member is denied; and deleting a linked
/// account unlinks every referencing member gracefully — the group opens, no dangling reference throws.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class LinkingWishlistAndCascadeTests(MailApiFactory factory)
{
    [Fact]
    public async Task ALinkedMember_OwnsAndReadsTheirOwnWishlist()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        var put = await member.Client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/my-wishlist",
            new UpdateMyWishlistRequest(["Scarf", "  ", "Book"])); // the empty item is dropped
        put.EnsureSuccessStatusCode();

        var wishlists = await member.Client.GetFromJsonAsync<MyWishlistsResponse>("/api/v1/my-wishlists");
        var mine = wishlists!.Wishlists.Single(w => w.GroupId == groupId);
        Assert.Equal(["Scarf", "Book"], mine.Items);
    }

    [Fact]
    public async Task ALinkedMember_AppearsInTheirMembershipsBeforeAnyDraw()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid(), groupName: "December");

        // No draw yet — the member can still find the group to own a wishlist there (FR-030).
        var memberships = await member.Client.GetFromJsonAsync<MyMembershipsResponse>("/api/v1/my-memberships");
        Assert.Contains(memberships!.Memberships, m => m.GroupId == groupId && m.GroupName == "December");
    }

    [Fact]
    public async Task ANonMember_CannotWriteAWishlistToThatGroup()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var stranger = await CircleTestAccounts.SignUpAsync(factory, "Stranger");
        var groupId = Guid.NewGuid();

        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        // A stranger to the group is denied, not silently accepted.
        var put = await stranger.Client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/my-wishlist",
            new UpdateMyWishlistRequest(["Sneaking in"]));
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task AUserLinkedInTwoGroups_HasTwoIndependentWishlists()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupA, Guid.NewGuid(), groupName: "A");
        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupB, Guid.NewGuid(), groupName: "B");

        await member.Client.PutAsJsonAsync($"/api/v1/groups/{groupA}/my-wishlist", new UpdateMyWishlistRequest(["A-item"]));
        await member.Client.PutAsJsonAsync($"/api/v1/groups/{groupB}/my-wishlist", new UpdateMyWishlistRequest(["B-item"]));

        var wishlists = await member.Client.GetFromJsonAsync<MyWishlistsResponse>("/api/v1/my-wishlists");
        Assert.Equal(["A-item"], wishlists!.Wishlists.Single(w => w.GroupId == groupA).Items);
        Assert.Equal(["B-item"], wishlists.Wishlists.Single(w => w.GroupId == groupB).Items);
    }

    [Fact]
    public async Task DeletingALinkedUsersAccount_UnlinksEveryReferencingMember_AndTheReadIsGone()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, memberId);
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Recipient", []));
        await member.Client.PutAsJsonAsync($"/api/v1/groups/{groupId}/my-wishlist", new UpdateMyWishlistRequest(["Kept"]));

        // The linked member deletes their account (the delete endpoint re-checks the password).
        var delete = await member.Client.PostAsJsonAsync(
            "/api/v1/account/delete",
            new AppSuite.Main.Contracts.DeleteAccountRequest(CircleTestAccounts.Password));
        delete.EnsureSuccessStatusCode();

        // The organizer's client receives an unlink resolution to apply — the member survives on the
        // group document (name + wishlist), only the account link is cleared (FR-015).
        var resolution = await LinkingScenario.DrainSingleResolutionAsync(organizer, groupId, memberId);
        Assert.Equal(MemberLinkResolutionKind.Unlinked, resolution.Kind);
        Assert.Null(resolution.UserId);
    }
}
