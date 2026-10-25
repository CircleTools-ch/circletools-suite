using Microsoft.Playwright;
using static CircleTools.SecretSanta.E2E.Test.GroupJourney;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Online groups with accounts (ADR-0086), end to end in Chromium against the live stack: an email invitation is
/// answered from the group list; a group organized without an account is kept in the account after signing in and
/// handed back; an account's group reaches its other device and a delete leaves it there too; and deleting the
/// account offers what becomes of the groups it owns.
/// </summary>
[Collection(SecretSantaE2E.CollectionName)]
public sealed class AccountGroupTests : IAsyncLifetime
{
    private const string Password = "correct-horse-battery-staple";
    private static readonly LocatorWaitForOptions Shown = new() { Timeout = 20_000 };
    private static readonly string[] OthersThanBen = ["Ada", "Cleo"];

    private E2EBrowser _browser = default!;

    async Task IAsyncLifetime.InitializeAsync() => _browser = await E2EBrowser.LaunchAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _browser.DisposeAsync();

    [Fact]
    public async Task An_invitation_is_accepted_from_the_group_list_and_opens_the_participant_page()
    {
        var organizer = await SignedInPersonAsync("Ada");
        var benEmail = UniqueEmail();
        var ben = await _browser.NewPersonAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(ben, benEmail, Password, "Ben");

        var groupId = await CreateGroupAsync(organizer, "E2E Invitation", Online);
        await AddMembersAsync(organizer, groupId, "Ben", "Cleo");
        await AddEventAndDrawAsync(organizer, groupId);
        await InviteAsync(organizer, groupId, "Ben", benEmail);

        await GotoAsync(ben, "/");
        await ReloadUntilAsync(ben, async () => await ben.Locator("#invitations").CountAsync() > 0);
        await ben.Locator("#invitations").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Take part", Exact = true }).ClickAsync();
        var takingPart = ben.Locator("#taking-part");
        await takingPart.GetByText("E2E Invitation").WaitForAsync(Shown);

        await takingPart.GetByText("E2E Invitation").ClickAsync();
        await ben.WaitForURLAsync($"**/groups/{groupId}/me", new PageWaitForURLOptions { Timeout = 20_000 });
        Assert.Contains(await RevealAsync(ben), OthersThanBen); // Ada is in the roster as its organizer

        // The organizer sees Ben connected.
        await GotoAsync(organizer, $"/groups/{groupId}/members");
        await ReloadUntilAsync(organizer, async () => await MemberRow(organizer, "Ben").GetByText("Connected").CountAsync() > 0);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task A_group_organized_without_an_account_is_kept_in_the_account_and_handed_back()
    {
        var page = await _browser.NewPersonAsync();
        var groupId = await CreateGroupAsync(page, "E2E Take over", Online);
        await GotoAsync(page, $"/groups/{groupId}/sharing");
        await page.Locator("#admin-link-card").WaitForAsync(Shown);

        await AccountSignIn.RegisterVerifyAndSignInAsync(page, UniqueEmail(), Password, "Dana");

        // Signed in, the group offers to move into the account; afterwards the management link is gone.
        await GotoAsync(page, $"/groups/{groupId}/sharing");
        await page.Locator("#keep-in-account").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Keep in my account" }).ClickAsync();
        await ConfirmAsync(page, "Keep in my account");
        await page.Locator("#account-owned").WaitForAsync(Shown);
        Assert.Equal(0, await page.Locator("#admin-link-card").CountAsync());

        // Handing it back to a link brings a fresh management link.
        await page.ClickAsync("#release-to-link");
        await ConfirmAsync(page, "Hand over");
        await page.Locator("#admin-link-card").WaitForAsync(Shown);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task An_accounts_group_reaches_its_other_device_and_a_delete_reaches_it_too()
    {
        var email = UniqueEmail();
        var phone = await _browser.NewPersonAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(phone, email, Password, "Eli");
        var laptop = await _browser.NewPersonAsync();
        await AccountSignIn.SignInAsync(laptop, email, Password);

        var groupId = await CreateGroupAsync(phone, "E2E Two devices", Online);
        await GotoAsync(laptop, "/");
        await ReloadUntilAsync(laptop, async () => await laptop.GetByText("E2E Two devices").CountAsync() > 0);

        // Deleted on the phone (online, for everyone), it goes from the laptop as well.
        await GotoAsync(phone, $"/groups/{groupId}");
        await phone.Locator("#group-menu").ClickAsync();
        await phone.Locator(".mud-popover-open .mud-menu-item", new PageLocatorOptions { HasText = "Delete" }).ClickAsync();
        await ConfirmAsync(phone, "Yes, delete");
        await phone.WaitForURLAsync($"{SecretSantaStackFixture.WebOrigin}/", new PageWaitForURLOptions { Timeout = 20_000 });

        await GotoAsync(laptop, "/");
        await ReloadUntilAsync(laptop, async () => await laptop.GetByText("E2E Two devices").CountAsync() == 0);

        _browser.AssertCleanConsole();
    }

    [Fact]
    public async Task Deleting_the_account_keeps_its_groups_online_for_the_people_taking_part()
    {
        var email = UniqueEmail();
        var owner = await _browser.NewPersonAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(owner, email, Password, "Finn");
        var groupId = await CreateGroupAsync(owner, "E2E Account deletion", Online);
        await AddMembersAsync(owner, groupId, "Gus", "Hanna");
        var joinUrl = await TurnOnJoinLinkAsync(owner, groupId);

        // Wait until the roster has reached the server: a visitor to the join link sees Gus.
        var guest = await _browser.NewPersonAsync();
        await guest.GotoAsync(joinUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await ReloadUntilAsync(guest, async () => await guest.Locator("#join-panel").GetByText("Gus", new LocatorGetByTextOptions { Exact = true }).CountAsync() > 0);

        // The delete screen asks what becomes of the online group; the default keeps it online.
        await GotoAsync(owner, "/account/data");
        var disposition = owner.Locator("#delete-shared-disposition");
        await disposition.WaitForAsync(Shown);
        Assert.Equal(2, await disposition.Locator(".mud-radio").CountAsync());
        await owner.Locator("#delete-password").FillAsync(Password);
        var deleted = owner.WaitForResponseAsync(r => r.Url.Contains("/account/delete", StringComparison.Ordinal));
        await owner.Locator(".circletools-danger form button[type=submit]").ClickAsync();
        Assert.Equal(204, (await deleted).Status);

        // Someone joining afterwards still finds the group.
        await JoinAsync(guest, joinUrl, "Gus");

        _browser.AssertCleanConsole();
    }

    private async Task<IPage> SignedInPersonAsync(string name)
    {
        var page = await _browser.NewPersonAsync();
        await AccountSignIn.RegisterVerifyAndSignInAsync(page, UniqueEmail(), Password, name);
        return page;
    }

    private static string UniqueEmail() => $"e2e-{Guid.NewGuid():N}@example.test";
}
