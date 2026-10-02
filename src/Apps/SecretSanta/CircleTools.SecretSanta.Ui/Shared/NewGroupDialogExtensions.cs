using CircleTools.SecretSanta.LocalStore;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>The name and chosen storage mode a <see cref="Shared.NewGroupDialog"/> returns.</summary>
/// <param name="Name">The trimmed group name.</param>
/// <param name="Mode">The storage mode chosen for this group.</param>
public sealed record NewGroupResult(string Name, StorageMode Mode);

/// <summary>Shows the phone "New group" sheet (ADR-0073): name + storage mode.</summary>
public static class NewGroupDialogExtensions
{
    /// <summary>
    /// Presents the create-group sheet, pre-selecting the current default storage mode.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="defaultMode">The default storage mode, pre-selected and shown.</param>
    /// <param name="accountEnabled">Whether "My account" is selectable (a session is live).</param>
    /// <returns>The name and chosen mode, or <see langword="null"/> if cancelled.</returns>
    public static async Task<NewGroupResult?> ShowNewGroupDialogAsync(
        this IDialogService dialogs,
        StorageMode defaultMode,
        bool accountEnabled)
    {
        var parameters = new DialogParameters<Shared.NewGroupDialog>
        {
            { x => x.Mode, defaultMode },
            { x => x.AccountEnabled, accountEnabled },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.Center,
            CloseOnEscapeKey = true,
        };

        var dialog = await dialogs.ShowAsync<Shared.NewGroupDialog>(title: null, parameters, options);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: NewGroupResult created } ? created : null;
    }
}
