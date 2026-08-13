using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// What a linked member sees of their own assignment (FR-029): their recipient, that recipient's
/// <em>current</em> wishlist, budget and event; and that a re-run shows the new pairing and never the
/// previous one.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class AssignmentReadTests(MailApiFactory factory)
{
    [Fact]
    public async Task ALinkedMember_SeesTheirOwnRecipientWishlistBudgetAndEvent()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Carol", ["Tea", "Book"]));

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;

        Assert.Equal("Carol", pairing.RecipientName);
        Assert.Equal(["Tea", "Book"], pairing.RecipientWishlist);
        Assert.Equal(25m, pairing.Budget!.Amount);
        Assert.Equal("CHF", pairing.Budget.Currency);
        Assert.Equal("Grandma's", pairing.Event!.Location);
    }

    [Fact]
    public async Task TheRecipientsCurrentWishlist_IsShown_NotTheDrawTimeSnapshot()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var giver = await CircleTestAccounts.SignUpAsync(factory, "Giver");
        var recipient = await CircleTestAccounts.SignUpAsync(factory, "Recipient");
        var groupId = Guid.NewGuid();
        var recipientMemberId = Guid.NewGuid();

        var giverAccount = await LinkingScenario.LinkMemberAsync(factory, organizer, giver, groupId, Guid.NewGuid(), memberDisplayName: "Giver");
        await LinkingScenario.LinkMemberAsync(factory, organizer, recipient, groupId, recipientMemberId, memberDisplayName: "Recipient");

        // Published with the recipient's wishlist as the organizer held it at draw time.
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(giverAccount, recipientMemberId, "Recipient", ["Old idea"]));

        // The recipient then edits their own wishlist.
        var put = await recipient.Client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/my-wishlist",
            new UpdateMyWishlistRequest(["Fresh idea", "Second idea"]));
        put.EnsureSuccessStatusCode();

        // The giver sees the recipient's CURRENT wishlist, composed at read time — not the snapshot.
        var response = await LinkingScenario.GetMyAssignmentAsync(giver, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;
        Assert.Equal(["Fresh idea", "Second idea"], pairing.RecipientWishlist);
    }

    [Fact]
    public async Task AfterAReDraw_TheLinkedMemberSeesTheNewPairing_AndNeverThePrevious()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "First recipient", []));
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 2,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Second recipient", []));

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;

        // The current pairing, never the superseded one.
        Assert.Equal("Second recipient", pairing.RecipientName);
    }

    [Fact]
    public async Task AStalePublish_ForAnOlderDrawVersion_DoesNotOverwriteTheCurrentPairing()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 2,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Current", []));
        // A late-arriving publish for an older draw must be ignored.
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Stale", []));

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;
        Assert.Equal("Current", pairing.RecipientName);
    }
}
