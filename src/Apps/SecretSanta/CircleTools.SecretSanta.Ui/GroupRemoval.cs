// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Deleting a group, or leaving one this device takes part in, from any group menu — the list and the group page
/// (Q34). Asks first, in words that say what the delete reaches (D6), then deletes or leaves. Both work offline:
/// the server is told on the next sync (ADR-0086).
/// </summary>
/// <param name="sender">Dispatches the delete.</param>
/// <param name="store">This device's group records, for the question and for leave vs. delete.</param>
/// <param name="online">Leaves a group this device takes part in.</param>
/// <param name="auth">The signed-in account.</param>
/// <param name="autoSync">Requests a sync after removing.</param>
/// <param name="haptics">The confirmation buzz on delete (FEAT-008 W3).</param>
/// <param name="dialogs">Shows the confirmation sheet.</param>
/// <param name="snackbar">Reports leaving.</param>
/// <param name="l">The app strings.</param>
public sealed class GroupRemoval(
    ISender sender,
    IGroupRecordStore store,
    OnlineGroupSync online,
    AuthenticationStateProvider auth,
    AutoSyncService autoSync,
    IHapticFeedback haptics,
    IDialogService dialogs,
    ISnackbar snackbar,
    IStringLocalizer<AppStrings> l)
{
    /// <summary>Whether this device only takes part in the group (removing it means leaving).</summary>
    /// <param name="groupId">The group.</param>
    /// <returns><see langword="true"/> for a group without the document on this device.</returns>
    public async Task<bool> IsJoinedAsync(Guid groupId) =>
        (await FindAsync(groupId)) is { Document: null };

    /// <summary>Asks, then deletes the group or leaves it.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="name">Its name, for the question.</param>
    /// <returns>Whether it was removed.</returns>
    public async Task<bool> AskAndRemoveAsync(Guid groupId, string name)
    {
        var record = await FindAsync(groupId);
        var joined = record is { Document: null };
        if (!await dialogs.ShowConfirmSheetAsync(Question(record, name), joined ? l["LeaveGroupConfirmYes"] : l["ConfirmYes"], l["Cancel"]))
        {
            return false;
        }

        if (joined)
        {
            var account = (await auth.GetAuthenticationStateAsync()).User.UserId();
            await online.LeaveAsync(groupId, account == Guid.Empty ? null : account);
            snackbar.Add(l["LeftGroup", name], Severity.Success);
            autoSync.RequestSync();
            return true;
        }

        await sender.Send(new DeleteGroupCommand(groupId));
        await haptics.TapAsync();
        return true;
    }

    // Say what the delete reaches (D6): an online group this device may delete goes for everyone; any other only
    // leaves this device.
    private string Question(GroupRecord? record, string name) => record switch
    {
        { Document: null } => l["LeaveGroupConfirm", name],
        { Place: GroupPlace.Online, Access: { } access } when access.Can(OnlinePermissions.DeleteGroup) => l["DeleteGroupOnlineConfirm", name],
        _ => l["ConfirmDeleteGroup", name],
    };

    private async Task<GroupRecord?> FindAsync(Guid groupId)
    {
        try
        {
            return (await store.LoadAsync()).Records.FirstOrDefault(r => r.Id == groupId);
        }
        catch (StorageUnavailableException)
        {
            return null;
        }
    }
}
