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
    /// Fills the inline create field, switches this group's storage to "Shared by link" via the Change
    /// menu, and clicks Create. The caller waits for the navigation to the new group.
    /// </summary>
    public static async Task CreateSharedGroupAsync(IPage page, string name)
    {
        await page.FillAsync("#new-group-name", name);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Change", Exact = true }).ClickAsync();
        await page.Locator(".mud-popover-open").GetByText("Shared by link", new LocatorGetByTextOptions { Exact = true }).ClickAsync();
        await page.GetByText("Stored:").Filter(new LocatorFilterOptions { HasTextString = "Shared by link" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.ClickAsync("#create-group-btn");
    }
}
