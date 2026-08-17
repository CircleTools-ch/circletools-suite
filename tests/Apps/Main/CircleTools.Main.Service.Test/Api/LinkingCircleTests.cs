using System.Net;
using System.Net.Http.Json;
using CircleTools.Circles.Contracts;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The create-from-circle direct-link path (FR-027): an organizer links an account-backed person they
/// share a circle with, without an invitation round-trip — the circle co-membership is the consent
/// (ADR-0018). The server verifies that shared-circle claim. This is the path the M4 browser
/// walkthrough exposed as missing: a person linked this way must be able to <strong>own their
/// wishlist</strong>, which needs a real server-side membership, not just a local <c>Member.UserId</c>.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class LinkingCircleTests(MailApiFactory factory)
{
    [Fact]
    public async Task RegisteringACircleLink_LetsTheLinkedMemberOwnTheirWishlist()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");

        // Both share a circle (the consent basis).
        var circle = await CircleFixture.CreateAsync(organizer, "Familie");
        await CircleFixture.AddMemberAsync(factory, organizer, member, circle.Id);
        var memberAccountId = await AccountIdOfOther(organizer, circle.Id);

        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        // Register the create-from-circle link server-side.
        var register = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links/circle",
            new RegisterCircleLinkRequest(groupId, "December", memberId, "Member", circle.Id, memberAccountId));
        register.EnsureSuccessStatusCode();

        // The organizer publishes the group's events so the member can enumerate them (ADR-0042).
        await LinkingScenario.RegisterDefaultEventAsync(organizer, groupId, "December");

        // The member is now a linked member: they appear in their memberships and can own a wishlist —
        // exactly what was broken when create-from-circle only set the local UserId.
        var memberships = await member.Client.GetFromJsonAsync<MyMembershipsResponse>("/api/v1/my-memberships");
        Assert.Contains(memberships!.Memberships, m => m.GroupId == groupId);

        var put = await LinkingScenario.PutMyWishlistAsync(member, groupId, ["Scarf"]);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
    }

    [Fact]
    public async Task RegisteringACircleLink_ForAnAccountNotInThatCircle_IsDenied()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var outsider = await CircleTestAccounts.SignUpAsync(factory, "Outsider");
        var circle = await CircleFixture.CreateAsync(organizer, "Familie");

        // The outsider is not a member of the circle, so their account id cannot be linked through it.
        var outsiderId = await AccountIdOfSelf(outsider);

        var register = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links/circle",
            new RegisterCircleLinkRequest(Guid.NewGuid(), "December", Guid.NewGuid(), "Ghost", circle.Id, outsiderId));

        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
    }

    private static async Task<Guid> AccountIdOfOther(CircleTestAccount viewer, Guid circleId)
    {
        var detail = await viewer.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circleId}");
        return detail!.People.Single(p => p is { IsYou: false, IsManaged: false }).AccountId!.Value;
    }

    private static async Task<Guid> AccountIdOfSelf(CircleTestAccount account)
    {
        // A fresh circle the account owns surfaces its own account id as the sole member.
        var circle = await CircleFixture.CreateAsync(account, "Solo");
        var detail = await account.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circle.Id}");
        return detail!.People.Single(p => p.IsYou).AccountId!.Value;
    }
}
