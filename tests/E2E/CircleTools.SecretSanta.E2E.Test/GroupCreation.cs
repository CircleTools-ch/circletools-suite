using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Creates a group from the groups screen the way a user does since ADR-0071 / ADR-0073: the storage mode
/// is no longer an always-open radio picker but a compact "Stored: … / Change" field, and a first visit
/// opens a one-time storage onboarding dialog (FR-048).
/// </summary>
/// <remarks>
/// The onboarding dialog is not what these tests exercise, and its backdrop blocks every click behind it,
/// so contexts that visit the groups screen add <see cref="SkipStorageOnboardingScript"/> as an init script
/// (marking onboarding as already shown, exactly as dismissing it would). Assumes the English UI and a
/// wide viewport (the inline create field is hidden on phones).
/// </remarks>
internal static class GroupCreation
{
    /// <summary>Init script that marks the one-time storage onboarding as already shown.</summary>
    public const string SkipStorageOnboardingScript =
        "try { localStorage.setItem('secretsanta.storage.onboarded','1'); } catch (e) {}";

    /// <summary>
    /// Fills the inline create field, switches this group's storage to the shared choice via the Change menu,
    /// and clicks Create. The caller waits for the navigation to the new group. Signed out the choice is
    /// "Shared by link"; signed in it is "My account · shared" — shared and owned by the account in one step
    /// (ADR-0081) — so whichever the menu offers is taken.
    /// </summary>
    public static Task CreateSharedGroupAsync(IPage page, string name) =>
        CreateGroupAsync(page, name, new Regex("^(Shared by link|My account · shared)$"));

    /// <summary>
    /// Fills the inline create field, switches this group's storage to <paramref name="storage"/> (its English
    /// title: "On this device", "Shared by link" or "My account") via the Change menu, and clicks Create.
    /// </summary>
    public static Task CreateGroupAsync(IPage page, string name, string storage) =>
        CreateGroupAsync(page, name, new Regex($"^{Regex.Escape(storage)}$"));

    private static async Task CreateGroupAsync(IPage page, string name, Regex storage)
    {
        await page.FillAsync("#new-group-name", name);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Change", Exact = true }).ClickAsync();
        await page.Locator(".mud-popover-open").GetByText(storage).First.ClickAsync();
        await page.GetByText("Stored:").Locator("strong").Filter(new LocatorFilterOptions { HasTextRegex = storage })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.ClickAsync("#create-group-btn");
    }
}
