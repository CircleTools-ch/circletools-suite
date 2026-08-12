using System.Net;
using System.Net.Http.Json;
using AppSuite.SecretSanta.Contracts;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Drives the real M4 linking endpoints two-account style, so the authorization tests never seed the
/// database and every call carries a genuine second bearer token. "Which account is this?" is the
/// property under test, so the acting account is an explicit parameter throughout (TP-005).
/// </summary>
internal static class LinkingScenario
{
    /// <summary>
    /// Links <paramref name="member"/> as a member of a group owned by <paramref name="organizer"/>,
    /// through the real send → accept flow, and returns the account id the organizer learned for the
    /// member from the link resolution (exactly how the organizer's client learns it in production).
    /// </summary>
    /// <returns>The member's account id, as carried by the drained <see cref="MemberLinkResolution"/>.</returns>
    public static async Task<Guid> LinkMemberAsync(
        MailApiFactory factory,
        CircleTestAccount organizer,
        CircleTestAccount member,
        Guid groupId,
        Guid memberId,
        string groupName = "December",
        string memberDisplayName = "Member")
    {
        var send = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links",
            new SendMemberLinkRequest(groupId, groupName, memberId, memberDisplayName, member.Email));
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        Assert.Equal(MemberLinkOutcome.RequestSent, (await send.Content.ReadFromJsonAsync<SendMemberLinkResponse>())!.Outcome);

        var pending = await member.Client.GetFromJsonAsync<IReadOnlyList<PendingMemberLinkResponse>>("/api/v1/member-links/pending");
        var request = pending!.Single(p => p.GroupId == groupId);

        var accept = await member.Client.PostAsync(new Uri($"/api/v1/member-links/{request.Id}/accept", UriKind.Relative), content: null);
        accept.EnsureSuccessStatusCode();
        Assert.Equal(AcceptMemberLinkOutcome.Linked, (await accept.Content.ReadFromJsonAsync<AcceptMemberLinkResponse>())!.Outcome);

        var resolution = await DrainSingleResolutionAsync(organizer, groupId, memberId);
        Assert.Equal(MemberLinkResolutionKind.Linked, resolution.Kind);
        Assert.NotNull(resolution.UserId);
        return resolution.UserId!.Value;
    }

    /// <summary>Drains exactly one resolution for a group/member and acknowledges it.</summary>
    public static async Task<MemberLinkResolution> DrainSingleResolutionAsync(
        CircleTestAccount organizer,
        Guid groupId,
        Guid memberId)
    {
        var resolutions = await organizer.Client
            .GetFromJsonAsync<MemberLinkResolutionsResponse>("/api/v1/member-links/resolutions");
        var resolution = resolutions!.Resolutions.Single(r => r.GroupId == groupId && r.MemberId == memberId);

        var ack = await organizer.Client.PostAsJsonAsync(
            "/api/v1/member-links/resolutions/ack",
            new AckMemberLinkResolutionsRequest([resolution.Id]));
        ack.EnsureSuccessStatusCode();
        return resolution;
    }

    /// <summary>Publishes the assignment projection for a group, as the organizer's client does after a draw.</summary>
    public static async Task PublishAsync(
        CircleTestAccount organizer,
        Guid groupId,
        long drawVersion,
        params PublishedAssignment[] assignments)
    {
        var publish = await organizer.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/assignments/publish",
            new PublishAssignmentsRequest(
                groupId,
                "December",
                drawVersion,
                new BudgetDto(25m, "CHF"),
                new EventDetailsDto(new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), "Grandma's", "Bring dessert"),
                assignments));
        publish.EnsureSuccessStatusCode();
    }

    /// <summary>Reads a caller's own assignment, returning the raw response so status codes can be asserted.</summary>
    public static Task<HttpResponseMessage> GetMyAssignmentAsync(CircleTestAccount caller, Guid groupId) =>
        caller.Client.GetAsync(new Uri($"/api/v1/groups/{groupId}/my-assignment", UriKind.Relative));
}
