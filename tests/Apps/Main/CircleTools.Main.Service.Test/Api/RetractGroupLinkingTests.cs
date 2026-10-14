using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Retracting a deleted group's linking projection (ADR-0077): when the organizer deletes a group, the
/// client calls <c>DELETE /groups/{id}/linking</c>. The group must then leave every linked member's
/// My assignments / My memberships / My wishlists. Only the organizer can retract, and the call is
/// idempotent.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class RetractGroupLinkingTests(MailApiFactory factory)
{
    [Fact]
    public async Task TheOrganizer_RetractsTheGroup_AndItLeavesTheLinkedMembersScreens()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var memberAccount = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, memberId);
        await LinkingScenario.RegisterDefaultEventAsync(organizer, groupId);
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccount, Guid.NewGuid(), "Bruno", ["Book"], string.Empty));
        (await LinkingScenario.PutMyWishlistAsync(member, groupId, ["Scarf"])).EnsureSuccessStatusCode();

        // Before: the member sees the group everywhere.
        Assert.Contains((await MyAssignmentsAsync(member)).Assignments, a => a.GroupId == groupId);

        var retract = await organizer.Client.DeleteAsync(new Uri($"/api/v1/groups/{groupId}/linking", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, retract.StatusCode);

        // After: gone from assignments, memberships and wishlists.
        Assert.DoesNotContain((await MyAssignmentsAsync(member)).Assignments, a => a.GroupId == groupId);
        var memberships = await member.Client.GetFromJsonAsync<MyMembershipsResponse>("/api/v1/my-memberships");
        Assert.DoesNotContain(memberships!.Memberships, m => m.GroupId == groupId);
        var wishlists = await member.Client.GetFromJsonAsync<MyWishlistsResponse>("/api/v1/my-wishlists");
        Assert.DoesNotContain(wishlists!.Wishlists, w => w.GroupId == groupId);

        // Idempotent: a second retract (a retried queue entry) is still a quiet success.
        var again = await organizer.Client.DeleteAsync(new Uri($"/api/v1/groups/{groupId}/linking", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task AStranger_CannotRetractSomeoneElsesGroup()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var stranger = await CircleTestAccounts.SignUpAsync(factory, "Stranger");
        var groupId = Guid.NewGuid();

        var memberAccount = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());
        await LinkingScenario.RegisterDefaultEventAsync(organizer, groupId);
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccount, Guid.NewGuid(), "Bruno", [], string.Empty));

        // Same 204 as a real retract (no oracle for whether the group exists), but nothing is deleted.
        var retract = await stranger.Client.DeleteAsync(new Uri($"/api/v1/groups/{groupId}/linking", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, retract.StatusCode);

        Assert.Contains((await MyAssignmentsAsync(member)).Assignments, a => a.GroupId == groupId);
    }

    private static async Task<MyAssignmentsResponse> MyAssignmentsAsync(CircleTestAccount caller) =>
        (await caller.Client.GetFromJsonAsync<MyAssignmentsResponse>("/api/v1/my-assignments"))!;
}
