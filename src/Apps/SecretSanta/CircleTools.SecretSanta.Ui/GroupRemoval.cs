using CircleTools.Application;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Deleting a group, or leaving a joined one, from any group menu — the list and the group page (Q34). Asks
/// first, in words that say what the delete reaches (D6), then deletes or leaves.
/// </summary>
/// <param name="sender">Dispatches the delete.</param>
/// <param name="claimable">Describes the group's storage, for the question and for leave vs. delete.</param>
/// <param name="sharedSync">Releases a joined group's claim online when leaving (ADR-0080).</param>
/// <param name="autoSync">Requests a sync after leaving.</param>
/// <param name="haptics">The confirmation buzz on delete (FEAT-008 W3).</param>
/// <param name="dialogs">Shows the confirmation sheet.</param>
/// <param name="snackbar">Reports leaving.</param>
/// <param name="l">The app strings.</param>
public sealed class GroupRemoval(
    ISender sender,
    ClaimableGroupsService claimable,
    SharedSyncService sharedSync,
    AutoSyncService autoSync,
    IHapticFeedback haptics,
    IDialogService dialogs,
    ISnackbar snackbar,
    IStringLocalizer<AppStrings> l)
{
    /// <summary>Whether the group was joined through a join link here — it is left, not deleted.</summary>
    /// <param name="groupId">The group.</param>
    /// <returns><see langword="true"/> for a join-only group.</returns>
    public async Task<bool> IsJoinedAsync(Guid groupId) =>
        (await claimable.GetAllAsync()).FirstOrDefault(g => g.Id == groupId) is { IsJoinOnly: true };

    /// <summary>Asks, then deletes the group or leaves it.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="name">Its name, for the question.</param>
    /// <returns><see langword="true"/> when the group is gone from this device.</returns>
    public async Task<bool> AskAndRemoveAsync(Guid groupId, string name)
    {
        var described = (await claimable.GetAllAsync()).FirstOrDefault(g => g.Id == groupId);
        var joined = described is { IsJoinOnly: true };
        if (!await dialogs.ShowConfirmSheetAsync(Question(described, name), joined ? l["LeaveGroupConfirmYes"] : l["ConfirmYes"], l["Cancel"]))
        {
            return false;
        }

        if (joined)
        {
            // Leaving releases the participant's claim online and removes the group here; it needs the server.
            if (!await sharedSync.LeaveAsync(groupId))
            {
                snackbar.Add(l["LeaveGroupOffline"], Severity.Warning);
                return false;
            }

            snackbar.Add(l["LeftGroup", name], Severity.Success);
            autoSync.RequestSync();
            return true;
        }

        await sender.Send(new DeleteGroupCommand(groupId));
        await haptics.TapAsync();
        return true;
    }

    // Say what the delete reaches (D6): an account group goes from every device of the account, an
    // admin-claimed shared group goes online for everyone (ADR-0078), and a joined group only leaves this
    // device - the organizer keeps it.
    private string Question(ClaimableGroup? described, string name) => described switch
    {
        { IsJoinOnly: true } => l["LeaveGroupConfirm", name],
        { AccountScope: not null } => l["DeleteGroupAccountConfirm", name],
        { Mode: StorageMode.Account, IsAwaitingClaim: false } => l["DeleteGroupOnlineConfirm", name],
        _ => l["ConfirmDeleteGroup", name],
    };
}
