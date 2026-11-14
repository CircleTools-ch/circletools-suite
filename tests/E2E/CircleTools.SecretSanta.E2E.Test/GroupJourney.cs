// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// The steps of a Secret-Santa journey the way a user takes them in the English UI (ADR-0086): create a group in a
/// chosen place, add members and an event, draw, turn the join link on, invite by email, join, and read one's own
/// assignment. Each step waits for what the user would see before the next one.
/// </summary>
internal static partial class GroupJourney
{
    /// <summary>The place "Online" in the create field.</summary>
    public const string Online = "Online";

    /// <summary>The place "On this device" in the create field.</summary>
    public const string OnThisDevice = "On this device";

    private static readonly LocatorWaitForOptions Shown = new() { Timeout = 20_000 };

    /// <summary>Opens a path in the Secret-Santa app and waits for the network to settle.</summary>
    /// <param name="page">The page.</param>
    /// <param name="path">The path, starting with <c>/</c>.</param>
    /// <returns>A task.</returns>
    public static Task GotoAsync(IPage page, string path) =>
        page.GotoAsync($"{SecretSantaStackFixture.WebOrigin}{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    /// <summary>Creates a group from the group list in <paramref name="place"/> and waits for its page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The group name.</param>
    /// <param name="place"><see cref="Online"/> or <see cref="OnThisDevice"/>.</param>
    /// <returns>The group id.</returns>
    public static async Task<string> CreateGroupAsync(IPage page, string name, string place)
    {
        await GotoAsync(page, "/");
        await page.FillAsync("#new-group-name", name);

        // The first group on a device shows both places; later ones a compact field with a Change menu.
        var expanded = page.Locator("#storage-expanded");
        if (await expanded.CountAsync() > 0)
        {
            await expanded.GetByText(place, new LocatorGetByTextOptions { Exact = true }).ClickAsync();
        }
        else
        {
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Change", Exact = true }).ClickAsync();
            await page.Locator(".mud-popover-open").GetByText(place, new LocatorGetByTextOptions { Exact = true }).First.ClickAsync();
        }

        await page.ClickAsync("#create-group-btn");
        await page.WaitForURLAsync(GroupPage(), new PageWaitForURLOptions { Timeout = 20_000 });
        return GroupPage().Match(page.Url).Groups[1].Value;
    }

    /// <summary>Adds members on the members page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="names">The members.</param>
    /// <returns>A task.</returns>
    public static async Task AddMembersAsync(IPage page, string groupId, params string[] names)
    {
        await GotoAsync(page, $"/groups/{groupId}/members");
        foreach (var name in names)
        {
            await page.FillAsync("#new-member-name", name);
            await page.ClickAsync("#add-member-btn");
            await MemberRow(page, name).WaitForAsync(Shown);
        }
    }

    /// <summary>Adds an event from the group page and runs the draw.</summary>
    /// <param name="page">The page.</param>
    /// <param name="groupId">The group.</param>
    /// <returns>The event page's path.</returns>
    public static async Task<string> AddEventAndDrawAsync(IPage page, string groupId)
    {
        await GotoAsync(page, $"/groups/{groupId}");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add event" }).Last.ClickAsync();
        var dialog = page.Locator(".mud-dialog");
        await dialog.WaitForAsync(Shown);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add event" }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/events/[0-9a-fA-F-]+$"), new PageWaitForURLOptions { Timeout = 20_000 });
        var eventPath = new Uri(page.Url).AbsolutePath;

        await GotoAsync(page, $"{eventPath}/draw");
        await page.ClickAsync("#draw-btn");
        await ExpectAsync(page, page.Locator("#go-share"), "drawn");
        return eventPath;
    }

    /// <summary>Turns the join link on in Sharing &amp; sync and returns it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="groupId">The group.</param>
    /// <returns>The join link.</returns>
    public static async Task<string> TurnOnJoinLinkAsync(IPage page, string groupId)
    {
        await GotoAsync(page, $"/groups/{groupId}/sharing");
        var card = page.Locator("#join-link-card");
        await card.WaitForAsync(Shown);
        var copy = card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Copy", Exact = true });
        if (await copy.CountAsync() == 0)
        {
            await page.Locator("#join-link-switch").ClickAsync();
            await copy.WaitForAsync(Shown);
        }

        var url = await SharingLinks.CopyLinkAsync(page, "#join-link-card");
        Assert.Matches(@"https?://localhost:7081/join#t=\S+", url);
        return url;
    }

    /// <summary>Invites an email address for a member, from the member's row menu.</summary>
    /// <param name="page">The page.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="member">The member.</param>
    /// <param name="email">The address.</param>
    /// <returns>A task.</returns>
    public static async Task InviteAsync(IPage page, string groupId, string member, string email)
    {
        await GotoAsync(page, $"/groups/{groupId}/members");
        await OpenRowMenuAsync(page, member, "Invite by email…");
        await page.Locator("input[type=email]").FillAsync(email);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Send invitation", Exact = true }).ClickAsync();
        await ExpectAsync(page, MemberRow(page, member).GetByText("Invited"), $"invited-{member}");
    }

    /// <summary>Frees a member's spot, from the member's row menu.</summary>
    /// <param name="page">The page.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="member">The member.</param>
    /// <returns>A task.</returns>
    public static async Task FreeSpotAsync(IPage page, string groupId, string member)
    {
        await GotoAsync(page, $"/groups/{groupId}/members");
        await MemberRow(page, member).GetByText("Connected").WaitForAsync(Shown);
        await OpenRowMenuAsync(page, member, "Free this spot");
        await ConfirmAsync(page, "Free the spot");
        await ReloadUntilAsync(page, async () => await MemberRow(page, member).GetByText("Connected").CountAsync() == 0);
    }

    /// <summary>Opens a join link and takes part as <paramref name="member"/>, on this device only.</summary>
    /// <param name="page">The page.</param>
    /// <param name="joinUrl">The join link.</param>
    /// <param name="member">The member to be.</param>
    /// <returns>A task.</returns>
    public static async Task JoinAsync(IPage page, string joinUrl, string member)
    {
        await page.GotoAsync(joinUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var panel = page.Locator("#join-panel");
        await ExpectAsync(page, panel.GetByText(member, new LocatorGetByTextOptions { Exact = true }), $"join-{member}");
        await panel.GetByText(member, new LocatorGetByTextOptions { Exact = true }).ClickAsync();
        await page.ClickAsync("#join-continue");
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F-]+/me$"), new PageWaitForURLOptions { Timeout = 20_000 });
        await ExpectAsync(page, page.Locator("#joined-as"), $"joined-{member}");
    }

    /// <summary>On the participant page, reveals the (single) event's assignment and returns whom one gives to.</summary>
    /// <param name="page">The page, on the participant page.</param>
    /// <returns>The recipient's name.</returns>
    public static async Task<string> RevealAsync(IPage page)
    {
        var reveal = page.Locator(".participant-reveal").First;
        await reveal.WaitForAsync(Shown);
        await reveal.ClickAsync();
        var recipient = page.Locator(".participant-recipient").First;
        await recipient.WaitForAsync(Shown);
        return (await recipient.InnerTextAsync()).Trim();
    }

    /// <summary>Reloads until <paramref name="ready"/> holds — for a change that arrives with the next sync.</summary>
    /// <param name="page">The page.</param>
    /// <param name="ready">The condition.</param>
    /// <returns>A task.</returns>
    public static async Task ReloadUntilAsync(IPage page, Func<Task<bool>> ready)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await page.WaitForTimeoutAsync(1_500);
            if (await ready())
            {
                return;
            }

            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
        }

        Assert.Fail($"The expected change did not arrive on {page.Url}.");
    }

    /// <summary>
    /// Waits for <paramref name="locator"/> to be shown; if it never is, saves a screenshot of the page under
    /// <c>e2e-screenshots</c> in the test output, so a failure in CI can be looked at.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="locator">What should be shown.</param>
    /// <param name="what">A short name for the screenshot.</param>
    /// <returns>A task.</returns>
    public static async Task ExpectAsync(IPage page, ILocator locator, string what)
    {
        try
        {
            await locator.WaitForAsync(Shown);
        }
        catch (TimeoutException)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "e2e-screenshots");
            Directory.CreateDirectory(dir);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, $"{what}-{DateTime.UtcNow:HHmmss}.png"), FullPage = true });
            throw;
        }
    }

    /// <summary>Answers an open confirmation sheet with <paramref name="answer"/>.</summary>
    /// <param name="page">The page.</param>
    /// <param name="answer">The button's label.</param>
    /// <returns>A task.</returns>
    public static Task ConfirmAsync(IPage page, string answer) =>
        page.Locator(".mud-dialog-container").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = answer, Exact = true }).ClickAsync();

    /// <summary>A member's row on the members page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="member">The member.</param>
    /// <returns>The row.</returns>
    public static ILocator MemberRow(IPage page, string member) =>
        page.Locator(".mud-paper, .mud-card").Filter(new LocatorFilterOptions
        {
            Has = page.Locator($"button[aria-label='More actions for {member}']"),
        }).Last;

    private static async Task OpenRowMenuAsync(IPage page, string member, string item)
    {
        await page.Locator($"button[aria-label='More actions for {member}']").ClickAsync();
        await page.Locator(".mud-popover-open .mud-menu-item", new PageLocatorOptions { HasText = item }).ClickAsync();
    }

    [GeneratedRegex(@"/groups/([0-9a-fA-F-]+)$")]
    private static partial Regex GroupPage();
}
