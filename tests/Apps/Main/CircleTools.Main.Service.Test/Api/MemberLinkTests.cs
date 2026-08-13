using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The member-link consent flow (FR-028): the enumeration-resistant request, self-link auto-accept,
/// the one-user-per-group guard, and that decline is indistinguishable from expiry to the organizer.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class MemberLinkTests(MailApiFactory factory)
{
    [Fact]
    public async Task SendingALinkRequest_Returns202RequestSent_WhetherOrNotTheAddressHasAnAccount()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");

        // To an address that has a (verified) account.
        var toAccount = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(Guid.NewGuid(), "December", Guid.NewGuid(), "Member", member.Email));

        // To an address that has none. The response must be identical — it reveals nothing (NFR-005).
        var toNobody = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(Guid.NewGuid(), "December", Guid.NewGuid(), "Ghost", $"nobody-{Guid.NewGuid():N}@example.test"));

        Assert.Equal(HttpStatusCode.Accepted, toAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, toNobody.StatusCode);
        Assert.Equal(MemberLinkOutcome.RequestSent, (await toAccount.Content.ReadFromJsonAsync<SendMemberLinkResponse>())!.Outcome);
        Assert.Equal(MemberLinkOutcome.RequestSent, (await toNobody.Content.ReadFromJsonAsync<SendMemberLinkResponse>())!.Outcome);
    }

    [Fact]
    public async Task AnOrganizerLinkingTheirOwnAccount_IsAutoAccepted()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var response = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(groupId, "December", memberId, "Me", organizer.Email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MemberLinkOutcome.SelfLinked, (await response.Content.ReadFromJsonAsync<SendMemberLinkResponse>())!.Outcome);

        // The self-link produces a Linked resolution the organizer's client applies — no second step.
        var resolution = await LinkingScenario.DrainSingleResolutionAsync(organizer, groupId, memberId);
        Assert.Equal(MemberLinkResolutionKind.Linked, resolution.Kind);
    }

    [Fact]
    public async Task ARequestLinkingAUserAlreadyLinkedInThatGroup_IsRefused()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid(), memberDisplayName: "First");

        // A second member of the SAME group, offered to the SAME account.
        var secondMemberId = Guid.NewGuid();
        await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(groupId, "December", secondMemberId, "Second", member.Email));
        var pending = await member.Client.GetFromJsonAsync<IReadOnlyList<PendingMemberLinkResponse>>("/api/v1/member-links/pending");
        var second = pending!.Single(p => p.MemberDisplayName == "Second");

        var accept = await member.Client.PostAsync(new Uri($"/api/v1/member-links/{second.Id}/accept", UriKind.Relative), content: null);
        accept.EnsureSuccessStatusCode();
        Assert.Equal(AcceptMemberLinkOutcome.AlreadyLinked, (await accept.Content.ReadFromJsonAsync<AcceptMemberLinkResponse>())!.Outcome);
    }

    [Fact]
    public async Task DeclineAndExpiry_AreIndistinguishableToTheOrganizer()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(groupId, "December", memberId, "Member", member.Email));
        var pending = await member.Client.GetFromJsonAsync<IReadOnlyList<PendingMemberLinkResponse>>("/api/v1/member-links/pending");
        var request = pending!.Single(p => p.GroupId == groupId);

        var decline = await member.Client.PostAsync(new Uri($"/api/v1/member-links/{request.Id}/decline", UriKind.Relative), content: null);
        decline.EnsureSuccessStatusCode();

        // A decline emits no resolution — exactly what an expiry would leave. The organizer cannot tell
        // "they said no" from "it lapsed": there is simply nothing to drain.
        var resolutions = await organizer.Client.GetFromJsonAsync<MemberLinkResolutionsResponse>("/api/v1/member-links/resolutions");
        Assert.DoesNotContain(resolutions!.Resolutions, r => r.GroupId == groupId && r.MemberId == memberId);
    }

    [Fact]
    public async Task Unlinking_RevertsTheMembersRead_AndEmitsAnUnlinkResolution()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, memberId);
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Recipient", []));

        // The linked member can read before the unlink.
        Assert.Equal(HttpStatusCode.OK, (await LinkingScenario.GetMyAssignmentAsync(member, groupId)).StatusCode);

        // The organizer unlinks by (group, member) — the pair their client naturally holds.
        var unlink = await organizer.Client.DeleteAsync(
            new Uri($"/api/v1/groups/{groupId}/members/{memberId}/link", UriKind.Relative));
        unlink.EnsureSuccessStatusCode();

        // The member's in-app assignment is gone — a graceful 404, NOT a 403 (they were a member, not a
        // stranger). The distinction is what makes the account-free path and unlink degrade without error.
        Assert.Equal(HttpStatusCode.NotFound, (await LinkingScenario.GetMyAssignmentAsync(member, groupId)).StatusCode);

        // The organizer's client receives an unlink resolution to apply to its group document.
        var resolution = await LinkingScenario.DrainSingleResolutionAsync(organizer, groupId, memberId);
        Assert.Equal(MemberLinkResolutionKind.Unlinked, resolution.Kind);
        Assert.Null(resolution.UserId);
    }
}
