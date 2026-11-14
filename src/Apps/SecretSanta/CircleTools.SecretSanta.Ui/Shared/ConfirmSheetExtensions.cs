// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Shows a <see cref="ConfirmSheet"/> as a bottom sheet and resolves to the user's choice (FEAT-008 W4).
/// </summary>
public static class ConfirmSheetExtensions
{
    /// <summary>
    /// Presents a destructive-action confirmation as a bottom sheet and returns whether the user
    /// confirmed. A backdrop click or Escape counts as cancel.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="message">The question/warning to show.</param>
    /// <param name="confirmLabel">Label for the confirming (destructive) action.</param>
    /// <param name="cancelLabel">Label for the dismissing action.</param>
    /// <param name="confirmColor">Colour of the confirm button (Error by default).</param>
    /// <returns><see langword="true"/> if the user confirmed; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> ShowConfirmSheetAsync(
        this IDialogService dialogs,
        string message,
        string confirmLabel,
        string cancelLabel,
        Color confirmColor = Color.Error)
    {
        var parameters = new DialogParameters<ConfirmSheet>
        {
            { x => x.Message, message },
            { x => x.ConfirmLabel, confirmLabel },
            { x => x.CancelLabel, cancelLabel },
            { x => x.ConfirmColor, confirmColor },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.BottomCenter,
            FullWidth = true,
            CloseOnEscapeKey = true,
        };

        var reference = await dialogs.ShowAsync<ConfirmSheet>(title: null, parameters, options);
        var result = await reference.Result;
        return result is { Canceled: false, Data: true };
    }
}
