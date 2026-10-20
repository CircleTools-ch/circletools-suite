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
    public async Task ALinkedMember_SeesTheBudgetRange_AndTheRecipientsGiftNote()
    {
        // FR-036 (budget min) + FR-037 (gift note): both must survive the server assignment projection and
        // reach the linked giver's my-assignment view, not only the account-free reveal link.
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        var publish = await organizer.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/assignments/publish",
            new PublishAssignmentsRequest(
                groupId,
                "December",
                groupId,
                "December event",
                DrawVersion: 1,
                new BudgetDto(30m, "CHF", MinAmount: 10m),
                new EventDetailsDto(null, string.Empty, string.Empty),
                [new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Carol", ["Tea"], RecipientGiftNote: "No nuts — allergy")]));
        publish.EnsureSuccessStatusCode();

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;

        Assert.Equal(30m, pairing.Budget!.Amount);
        Assert.Equal(10m, pairing.Budget.MinAmount);
        Assert.Equal("No nuts — allergy", pairing.RecipientGiftNote);
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

        // The recipient then edits their own wishlist (for the same default event the draw published).
        var put = await LinkingScenario.PutMyWishlistAsync(recipient, groupId, ["Fresh idea", "Second idea"]);
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
    public async Task ARePublishOfTheSameDraw_WithChangedContent_ReachesTheLinkedGiver()
    {
        // R1 (2026-10-20 device test): the organizer's client re-publishes the current draw every sync cycle.
        // A wishlist, gift note, budget or event detail edited after the draw must replace the draw-time copy.
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var recipientMemberId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        await PublishDrawAsync(organizer, groupId, memberAccountId, recipientMemberId, ["Lego"], "", 30m, "Grandma's");
        await PublishDrawAsync(organizer, groupId, memberAccountId, recipientMemberId, ["Lego", "Socks"], "No nuts", 40m, "Uncle's");

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;

        Assert.Equal(["Lego", "Socks"], pairing.RecipientWishlist);
        Assert.Equal("No nuts", pairing.RecipientGiftNote);
        Assert.Equal(40m, pairing.Budget!.Amount);
        Assert.Equal("Uncle's", pairing.Event!.Location);
    }

    [Fact]
    public async Task ARePublishOfTheSameDraw_WithUnchangedContent_KeepsThePairing()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();
        var recipientMemberId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        await PublishDrawAsync(organizer, groupId, memberAccountId, recipientMemberId, ["Lego"], "No nuts", 30m, "Grandma's");
        await PublishDrawAsync(organizer, groupId, memberAccountId, recipientMemberId, ["Lego"], "No nuts", 30m, "Grandma's");

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;

        Assert.Equal("Carol", pairing.RecipientName);
        Assert.Equal(["Lego"], pairing.RecipientWishlist);
        Assert.Equal("No nuts", pairing.RecipientGiftNote);
    }

    [Fact]
    public async Task AStalePublish_ForAnOlderDrawVersion_IsIgnored_EvenAfterTheCurrentDrawWasRePublished()
    {
        var organizer = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var member = await CircleTestAccounts.SignUpAsync(factory, "Member");
        var groupId = Guid.NewGuid();

        var memberAccountId = await LinkingScenario.LinkMemberAsync(factory, organizer, member, groupId, Guid.NewGuid());

        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 2,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Current", ["Old"]));
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 2,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Current", ["New"]));
        await LinkingScenario.PublishAsync(organizer, groupId, drawVersion: 1,
            new PublishedAssignment(memberAccountId, Guid.NewGuid(), "Stale", []));

        var response = await LinkingScenario.GetMyAssignmentAsync(member, groupId);
        var pairing = (await response.Content.ReadFromJsonAsync<MyAssignmentResponse>())!;
        Assert.Equal("Current", pairing.RecipientName);
        Assert.Equal(["New"], pairing.RecipientWishlist);
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

    private static async Task PublishDrawAsync(
        CircleTestAccount organizer,
        Guid groupId,
        Guid giverAccountId,
        Guid recipientMemberId,
        IReadOnlyList<string> wishlist,
        string giftNote,
        decimal budget,
        string location)
    {
        var publish = await organizer.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/assignments/publish",
            new PublishAssignmentsRequest(
                groupId,
                "December",
                groupId,
                "December event",
                DrawVersion: 1,
                new BudgetDto(budget, "CHF"),
                new EventDetailsDto(new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), location, string.Empty),
                [new PublishedAssignment(giverAccountId, recipientMemberId, "Carol", [.. wishlist], RecipientGiftNote: giftNote)]));
        publish.EnsureSuccessStatusCode();
    }
}
