// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Circles.Contracts;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Covers the two account-deletion seams end to end (FR-015) — the criterion M1 shipped the
/// extension point for and could not close until circles existed.
/// </summary>
/// <remarks>
/// These run against the real endpoints and real Postgres, because what is being tested is the
/// composition: a guard and a cleanup that are each trivial on their own, wired into the deletion
/// path in the right order.
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class AccountDeletionCleanupTests(MailApiFactory factory)
{
    private const string Password = "correct-horse-battery";

    private static Task<HttpResponseMessage> DeleteAccountAsync(CircleTestAccount account) =>
        account.Client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(Password));

    /// <summary>The people list, which is part of the circle rather than a route of its own.</summary>
    private static async Task<IReadOnlyList<CirclePersonResponse>> PeopleAsync(
        CircleTestAccount viewer,
        Guid circleId)
    {
        var circle = await viewer.Client
            .GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circleId}");

        return circle!.People;
    }

    private static async Task<Guid> MembershipOfAsync(
        CircleTestAccount viewer,
        Guid circleId,
        string displayName)
    {
        var people = await PeopleAsync(viewer, circleId);

        return people.Single(p => p.DisplayName == displayName).MembershipId;
    }

    private static async Task PromoteToAdminAsync(
        CircleTestAccount owner,
        CircleTestAccount target,
        Guid circleId)
    {
        var membershipId = await MembershipOfAsync(owner, circleId, target.DisplayName);

        var promoted = await owner.Client.PutAsJsonAsync(
            $"/api/v1/circles/{circleId}/memberships/{membershipId}/role",
            new ChangeRoleRequest("Admin"));

        promoted.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Deletion_IsRefusedWhileTheAccountOwnsACircle()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        await CircleFixture.CreateAsync(anna, "Müller");

        var response = await DeleteAccountAsync(anna);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // And the refusal is actionable: a translatable code, plus the circle by name and id.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("circles_still_owned", problem.GetProperty("code").GetString());

        var blocker = Assert.Single(problem.GetProperty("blockers").EnumerateArray().ToArray());
        Assert.Equal("Müller", blocker.GetProperty("name").GetString());
        Assert.Equal("owner_must_delete_circle", blocker.GetProperty("resolution").GetString());
    }

    /// <summary>
    /// The refusal has to survive the guard, not just be produced by it: a blocked deletion must
    /// leave the account signed in and intact, because ADR-0022 gives nothing back.
    /// </summary>
    [Fact]
    public async Task ARefusedDeletion_ChangesNothing()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");

        await DeleteAccountAsync(anna);

        var stillThere = await anna.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));
        stillThere.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WithOthersInTheCircle_TheRefusalOffersTransferInstead()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var response = await DeleteAccountAsync(anna);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var blocker = Assert.Single(problem.GetProperty("blockers").EnumerateArray().ToArray());
        Assert.Equal("owner_must_transfer_ownership", blocker.GetProperty("resolution").GetString());
    }

    [Fact]
    public async Task OnceOwnershipIsHandedOver_TheAccountCanBeDeleted()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var beasMembership = await MembershipOfAsync(anna, circle.Id, "Bea");

        var transferred = await anna.Client.PostAsync(
            new Uri(
                $"/api/v1/circles/{circle.Id}/memberships/{beasMembership}/transfer-ownership",
                UriKind.Relative),
            content: null);
        transferred.EnsureSuccessStatusCode();

        var deleted = await DeleteAccountAsync(anna);

        deleted.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeletingTheCircleFirst_UnblocksTheAccount()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");

        var removed = await anna.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative))
        {
            Content = JsonContent.Create(new DeleteCircleRequest("Müller")),
        });
        removed.EnsureSuccessStatusCode();

        var deleted = await DeleteAccountAsync(anna);

        deleted.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The cleanup seam. Without it the deleted account stays in Bea's people list forever as a row
    /// pointing at a user identifier that no longer resolves — a person who is there and is nobody.
    /// </summary>
    [Fact]
    public async Task Deletion_RemovesTheAccountFromOtherPeoplesCircles()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        Assert.Contains(await PeopleAsync(anna, circle.Id), p => p.DisplayName == "Bea");

        (await DeleteAccountAsync(bea)).EnsureSuccessStatusCode();

        var after = await PeopleAsync(anna, circle.Id);

        Assert.DoesNotContain(after, p => p.DisplayName == "Bea");
        Assert.Contains(after, p => p.DisplayName == "Anna");
    }

    /// <summary>
    /// FR-019: a managed member is circle data, not their creator's. The admin who added them can
    /// delete their own account without taking the grandparent out of the family circle.
    /// </summary>
    [Fact]
    public async Task Deletion_LeavesTheManagedMembersTheAccountAdded()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        // Bea has to be able to add one, so make her an admin first.
        await PromoteToAdminAsync(anna, bea, circle.Id);

        var added = await bea.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/managed-members",
            new AddManagedMemberRequest("Opa", null));
        added.EnsureSuccessStatusCode();

        (await DeleteAccountAsync(bea)).EnsureSuccessStatusCode();

        var after = await PeopleAsync(anna, circle.Id);

        Assert.DoesNotContain(after, p => p.DisplayName == "Bea");
        Assert.Contains(after, p => p.DisplayName == "Opa");
    }

    /// <summary>
    /// The other half of the cleanup: invitations the departing account issued go with it, so
    /// nothing is left offering a way into a circle on behalf of somebody who no longer exists.
    /// </summary>
    [Fact]
    public async Task Deletion_WithdrawsTheInvitationsTheAccountSent()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        await PromoteToAdminAsync(anna, bea, circle.Id);

        var invited = await bea.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/invitations",
            new InviteRequest("chris@example.test", ManagedMembershipId: null));
        invited.EnsureSuccessStatusCode();

        var pendingBefore = await anna.Client
            .GetFromJsonAsync<IReadOnlyList<PendingInvitationResponse>>($"/api/v1/circles/{circle.Id}/invitations");
        Assert.Single(pendingBefore!);

        (await DeleteAccountAsync(bea)).EnsureSuccessStatusCode();

        var pendingAfter = await anna.Client
            .GetFromJsonAsync<IReadOnlyList<PendingInvitationResponse>>($"/api/v1/circles/{circle.Id}/invitations");

        Assert.Empty(pendingAfter!);
    }
}
