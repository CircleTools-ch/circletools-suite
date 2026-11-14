// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using CircleTools.Circles.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Covers joining a circle <em>as</em> an existing offline person against the real stack: the admin
/// aiming an invitation at a managed member, the recipient picking themselves, and the address gate on
/// the offline-roster read (FR-019, ADR-0043).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class InvitationPromotionTests(MailApiFactory factory)
{
    [Fact]
    public async Task AdminTargetedInvitation_PromotesTheOfflineProfile_WithNoDuplicate()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var joiner = await CircleTestAccounts.SignUpAsync(factory, "Mia Meier");
        var circle = await CircleFixture.CreateAsync(owner, "Müller");

        var mia = await AddManagedAsync(owner, circle.Id, "Mia");
        var invitationId = await InviteAndFindRequestAsync(owner, joiner, circle.Id, mia.MembershipId);

        // The inviter aimed at Mia, so the recipient just accepts — no choice sent.
        var accepted = await joiner.Client.PostAsJsonAsync(
            $"/api/v1/invitations/{circle.Id}/{invitationId}/accept",
            new AcceptInvitationBody(ManagedMembershipId: null));
        accepted.EnsureSuccessStatusCode();

        var people = (await joiner.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circle.Id}"))!.People;

        Assert.Equal(2, people.Count);
        var promoted = Assert.Single(people, p => p.MembershipId == mia.MembershipId);
        Assert.False(promoted.IsManaged);
        Assert.True(promoted.IsYou);
        Assert.Equal("Mia Meier", promoted.DisplayName);
    }

    [Fact]
    public async Task UntargetedInvitation_LetsTheRecipientPickTheOfflineProfile()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var joiner = await CircleTestAccounts.SignUpAsync(factory, "Mia Meier");
        var circle = await CircleFixture.CreateAsync(owner, "Müller");

        var mia = await AddManagedAsync(owner, circle.Id, "Mia");
        var invitationId = await InviteAndFindRequestAsync(owner, joiner, circle.Id, targetManagedId: null);

        // Untargeted: the recipient is offered the offline people and picks Mia.
        var targets = await joiner.Client
            .GetFromJsonAsync<JoinTargetsResponse>($"/api/v1/invitations/{circle.Id}/{invitationId}/targets");
        Assert.Null(targets!.PreselectedMembershipId);
        Assert.Contains(targets.Options, o => o.MembershipId == mia.MembershipId && o.DisplayName == "Mia");

        var accepted = await joiner.Client.PostAsJsonAsync(
            $"/api/v1/invitations/{circle.Id}/{invitationId}/accept",
            new AcceptInvitationBody(mia.MembershipId));
        accepted.EnsureSuccessStatusCode();

        var people = (await joiner.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circle.Id}"))!.People;

        Assert.Equal(2, people.Count);
        var promoted = Assert.Single(people, p => p.MembershipId == mia.MembershipId);
        Assert.False(promoted.IsManaged);
        Assert.True(promoted.IsYou);
    }

    [Fact]
    public async Task Targets_ForAnAdminTargetedInvitation_PreselectItAndOfferNoChoice()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var joiner = await CircleTestAccounts.SignUpAsync(factory, "Mia Meier");
        var circle = await CircleFixture.CreateAsync(owner, "Müller");

        var mia = await AddManagedAsync(owner, circle.Id, "Mia");
        await AddManagedAsync(owner, circle.Id, "Tom");
        var invitationId = await InviteAndFindRequestAsync(owner, joiner, circle.Id, mia.MembershipId);

        var targets = await joiner.Client
            .GetFromJsonAsync<JoinTargetsResponse>($"/api/v1/invitations/{circle.Id}/{invitationId}/targets");

        Assert.Equal(mia.MembershipId, targets!.PreselectedMembershipId);
        Assert.Equal("Mia", targets.PreselectedDisplayName);
        Assert.Empty(targets.Options);
    }

    [Fact]
    public async Task Targets_ForAnAddressThatIsNotTheInvited_AreRefused()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var joiner = await CircleTestAccounts.SignUpAsync(factory, "Mia");
        var stranger = await CircleTestAccounts.SignUpAsync(factory, "Eve");
        var circle = await CircleFixture.CreateAsync(owner, "Müller");

        await AddManagedAsync(owner, circle.Id, "Mia");
        var invitationId = await InviteAndFindRequestAsync(owner, joiner, circle.Id, targetManagedId: null);

        // A signed-in account that is not the invited address is told exactly what an unknown
        // invitation is told — the roster never leaks (ADR-0043).
        var response = await stranger.Client.GetAsync(
            new Uri($"/api/v1/invitations/{circle.Id}/{invitationId}/targets", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PendingInvitations_ShowTheOfflineTargetsName()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var joiner = await CircleTestAccounts.SignUpAsync(factory, "Mia Meier");
        var circle = await CircleFixture.CreateAsync(owner, "Müller");

        var mia = await AddManagedAsync(owner, circle.Id, "Mia");
        await InviteAndFindRequestAsync(owner, joiner, circle.Id, mia.MembershipId);

        var pending = await owner.Client
            .GetFromJsonAsync<IReadOnlyList<PendingInvitationResponse>>($"/api/v1/circles/{circle.Id}/invitations");

        var entry = Assert.Single(pending!);
        Assert.Equal(mia.MembershipId, entry.ManagedMembershipId);
        Assert.Equal("Mia", entry.ManagedDisplayName);
    }

    private static async Task<CirclePersonResponse> AddManagedAsync(
        CircleTestAccount owner,
        Guid circleId,
        string displayName)
    {
        var response = await owner.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circleId}/managed-members",
            new AddManagedMemberRequest(displayName, Language: null));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CirclePersonResponse>())!;
    }

    private async Task<Guid> InviteAndFindRequestAsync(
        CircleTestAccount inviter,
        CircleTestAccount invitee,
        Guid circleId,
        Guid? targetManagedId)
    {
        var invited = await inviter.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circleId}/invitations",
            new InviteRequest(invitee.Email, targetManagedId));
        invited.EnsureSuccessStatusCode();

        await CircleTestAccounts.DispatchOutboxAsync(factory);

        var requests = await invitee.Client
            .GetFromJsonAsync<IReadOnlyList<JoinRequestResponse>>("/api/v1/invitations");

        return requests!.Single(r => r.CircleId == circleId).InvitationId;
    }
}
