using Microsoft.Playwright;
using static CircleTools.SecretSanta.E2E.Test.GroupJourney;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Reveal links end to end in Chromium against the live stack: a group kept on the device hands out links that work
/// for anyone, with no account (ADR-0014); an online group's short link shows the current state, and a wishlist
/// written from it reaches the giver at once (ADR-0086, ADR-0084).
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class RevealLinkTests : IAsyncLifetime
{
    private static readonly string[] Members = ["Anna", "Bruno", "Clara"];
    private static readonly string[] OthersThanAnna = ["Bruno", "Clara"];
    private static readonly string[] OthersThanClara = ["Anna", "Bruno"];
    private static readonly LocatorWaitForOptions Shown = new() { Timeout = 20_000 };

    private E2EBrowser _browser = default!;

    async Task IAsyncLifetime.InitializeAsync() => _browser = await E2EBrowser.LaunchAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _browser.DisposeAsync();

    [Fact]
    public async Task A_device_groups_link_reveals_one_pairing_to_anyone_with_it()
    {
        var organizer = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(organizer, "E2E On this device", OnThisDevice);
        await AddMembersAsync(organizer, groupId, Members);
        var eventPath = await AddEventAndDrawAsync(organizer, groupId);

        await GotoAsync(organizer, $"{eventPath}/share");
        var link = await SharingLinks.CopyRevealLinkAsync(organizer, "Anna");

        var anna = await _browser.NewPersonAsync();
        var recipient = await RevealAsync(anna, link);
        Assert.Contains(recipient, OthersThanAnna);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task An_online_groups_link_takes_a_wishlist_that_reaches_the_giver_at_once()
    {
        var organizer = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(organizer, "E2E Online links", Online);
        await AddMembersAsync(organizer, groupId, Members);
        var eventPath = await AddEventAndDrawAsync(organizer, groupId);

        await GotoAsync(organizer, $"{eventPath}/share");
        var links = new Dictionary<string, string>();
        foreach (var member in Members)
        {
            links[member] = await SharingLinks.CopyRevealLinkAsync(organizer, member);
            Assert.Contains("/r#", links[member], StringComparison.Ordinal);
        }

        // Clara writes her wishlist from her link; it saves when the field loses focus.
        var clara = await _browser.NewPersonAsync();
        await clara.GotoAsync(links["Clara"], new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var wishlist = clara.Locator("#reveal-own-wishlist");
        await wishlist.WaitForAsync(Shown);
        await wishlist.FillAsync("Puzzle with 1000 pieces");
        await clara.Locator("#reveal-own-gift-note").FocusAsync();
        await clara.GetByText("Saved").First.WaitForAsync(Shown);

        // Whoever gives to Clara sees it on their own link straight away.
        var giverSaw = false;
        foreach (var giver in OthersThanClara)
        {
            var page = await _browser.NewPersonAsync();
            if (await RevealAsync(page, links[giver]) == "Clara")
            {
                await page.GetByText("Puzzle with 1000 pieces").First.WaitForAsync(Shown);
                giverSaw = true;
            }
        }

        Assert.True(giverSaw, "Neither Anna nor Bruno gives to Clara.");
        _browser.AssertCleanConsole();
    }

    // Opens a reveal link, reveals it and returns the recipient's name as the page shows it.
    private static async Task<string> RevealAsync(IPage page, string link)
    {
        await page.GotoAsync(link, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Reveal who you got" }).ClickAsync();
        var result = page.Locator("#reveal-result");
        await result.WaitForAsync(Shown);
        var text = await result.InnerTextAsync();
        return Members.Single(m => text.Contains(m, StringComparison.Ordinal));
    }
}
