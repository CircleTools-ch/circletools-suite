using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The load-bearing level of TP-005: assignment authorization, verified with <strong>two genuine
/// accounts</strong>. A signed-in member may read only their own pairing; a request for anyone else's
/// is <strong>denied — not filtered to an empty result</strong>; and a non-organizer response never
/// carries more than one assignment. One wrong rule here leaks the exact secret the app exists to keep
/// (NFR-002, ADR-0020), so these are first-class assertions, not happy-path corollaries.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class AssignmentAuthorizationTests(MailApiFactory factory)
{
    [Fact]
    public async Task ARequestForAnotherMembersAssignment_Is403_NotAnEmptyResult()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var stranger = await CircleTestAccounts.SignUpAsync(factory, "Stranger");

        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, memberId);
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, recipientId, "Recipient", ["Tea"]));

        // The linked member reads their own pairing.
        var own = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        // A stranger to the group is DENIED — a 403, never a 200 with an empty body that reads the same
        // as "no draw yet". This is the assertion that would fail if the rule filtered instead of denied.
        var probed = await LinkingScenario.GetMyAssignmentAsync(stranger, groupId);
        Assert.Equal(HttpStatusCode.Forbidden, probed.StatusCode);
    }

    [Fact]
    public async Task ANonOrganizerResponse_ContainsAtMostOneAssignment()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var alice = await CircleTestAccounts.SignUpAsync(factory, "Alice");
        var bob = await CircleTestAccounts.SignUpAsync(factory, "Bob");

        var groupId = Guid.NewGuid();
        var aliceMemberId = Guid.NewGuid();
        var bobMemberId = Guid.NewGuid();

        var aliceAccount = await LinkingScenario.LinkMemberAsync(factory, organizer, alice, groupId, aliceMemberId, memberDisplayName: "Alice");
        var bobAccount = await LinkingScenario.LinkMemberAsync(factory, organizer, bob, groupId, bobMemberId, memberDisplayName: "Bob");

        // Two givers are published for the group; Alice must still receive exactly one — hers.
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(aliceAccount, bobMemberId, "Bob", []),
            new PublishedAssignment(bobAccount, aliceMemberId, "Alice", []));

        var single = await LinkingScenario.GetMyAssignmentAsync(alice, groupId);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        var pairing = (await single.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;
        Assert.Equal("Bob", pairing.RecipientName);

        // The cross-group overview is likewise capped at one row per group Alice is linked in.
        var all = await alice.Client.GetFromJsonAsync<MyAssignmentsResponse>("/api/v1/my-assignments");
        Assert.Single(all!.Assignments);
        Assert.Equal("Bob", all.Assignments[0].RecipientName);
    }

    [Fact]
    public async Task AMemberOfTheGroupWithNoDrawYet_Gets404_NotDenied()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");

        var groupId = Guid.NewGuid();
        await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        // A known member before any publish: nothing to show, but NOT a denial — 404, not 403.
        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
