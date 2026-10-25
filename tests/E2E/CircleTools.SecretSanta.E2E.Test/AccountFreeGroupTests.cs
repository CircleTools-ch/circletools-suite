using Microsoft.Playwright;
using static CircleTools.SecretSanta.E2E.Test.GroupJourney;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// The online group without any account (ADR-0086, decision D1), end to end in Chromium against the live stack:
/// the organizer's device holds the management link and a second device co-organizes through it; people take part
/// through the join link on their own device, see only their own pairing, and a freed spot can be taken again.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class AccountFreeGroupTests : IAsyncLifetime
{
    private static readonly string[] OthersThanAnna = ["Bruno", "Cleo"];

    private E2EBrowser _browser = default!;

    async Task IAsyncLifetime.InitializeAsync() => _browser = await E2EBrowser.LaunchAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _browser.DisposeAsync();

    [Fact]
    public async Task The_management_link_lets_a_second_device_co_organize()
    {
        var organizer = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(organizer, "E2E Co-organizing", Online);
        await organizer.Locator("#mode-chip").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        await GotoAsync(organizer, $"/groups/{groupId}/sharing");
        var managementLink = await SharingLinks.CopyLinkAsync(organizer, "#admin-link-card");
        Assert.Contains($"/admin#g={groupId}&t=", managementLink, StringComparison.Ordinal);

        // A second device opens the management link and lands in the group as an organizer.
        var coOrganizer = await _browser.NewPersonAsync();
        await coOrganizer.GotoAsync(managementLink, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await coOrganizer.WaitForURLAsync($"**/groups/{groupId}", new PageWaitForURLOptions { Timeout = 20_000 });
        await coOrganizer.GetByText("E2E Co-organizing").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        // An edit there reaches the first device.
        await AddMembersAsync(coOrganizer, groupId, "Cleo");
        await GotoAsync(organizer, $"/groups/{groupId}/members");
        await ReloadUntilAsync(organizer, async () => await MemberRow(organizer, "Cleo").CountAsync() > 0);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task A_participant_joins_on_their_device_and_sees_only_their_own_pairing()
    {
        var organizer = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(organizer, "E2E Join", Online);
        await AddMembersAsync(organizer, groupId, "Anna", "Bruno", "Cleo");
        await AddEventAndDrawAsync(organizer, groupId);
        var joinUrl = await TurnOnJoinLinkAsync(organizer, groupId);

        var anna = await _browser.NewPersonAsync();
        await JoinAsync(anna, joinUrl, "Anna");
        Assert.Contains("Anna", await anna.Locator("#joined-as").InnerTextAsync(), StringComparison.Ordinal);

        // The page shows Anna's own pairing: one of the others.
        Assert.Contains(await RevealAsync(anna), OthersThanAnna);

        // Her wishlist reaches the organizer's copy, marked as kept by her.
        await anna.Locator(".participant-wishlist textarea").First.FillAsync("A good book");
        await anna.ClickAsync("#save-own-entry");
        await anna.GetByText("Saved.").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        await GotoAsync(organizer, $"/groups/{groupId}/members");
        await ReloadUntilAsync(organizer, async () => await MemberRow(organizer, "Anna").GetByText("Connected").CountAsync() > 0);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task A_freed_spot_can_be_taken_again_from_another_device()
    {
        var organizer = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(organizer, "E2E Free a spot", Online);
        await AddMembersAsync(organizer, groupId, "Anna", "Bruno");
        var joinUrl = await TurnOnJoinLinkAsync(organizer, groupId);

        var lostPhone = await _browser.NewPersonAsync();
        await JoinAsync(lostPhone, joinUrl, "Anna");

        // Anna's spot is taken: the organizer frees it, and her new device takes it.
        await FreeSpotAsync(organizer, groupId, "Anna");
        var newPhone = await _browser.NewPersonAsync();
        await JoinAsync(newPhone, joinUrl, "Anna");
        Assert.Contains("Anna", await newPhone.Locator("#joined-as").InnerTextAsync(), StringComparison.Ordinal);

        _browser.AssertCleanConsole();
    }
}
