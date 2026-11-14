// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Shows a <see cref="TextInputSheet"/> as a bottom sheet and returns the entered text (FEAT-008 W4).
/// </summary>
public static class TextInputSheetExtensions
{
    /// <summary>
    /// Presents a single-field input as a bottom sheet (the form behind a FAB "add" action) and returns
    /// the trimmed text, or <see langword="null"/> if the user cancelled.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="title">The sheet title.</param>
    /// <param name="label">Floating label for the field.</param>
    /// <param name="confirmLabel">Label for the confirming action.</param>
    /// <param name="cancelLabel">Label for the dismissing action.</param>
    /// <param name="initial">Optional initial text (e.g. the current name when renaming).</param>
    /// <param name="hint">Optional line above the field (e.g. "Anna added - next name?" when adding several).</param>
    /// <param name="submitting">Applies the value in the sheet; an error it returns is shown under the field and the sheet stays open (Q10).</param>
    /// <param name="continueWith">Stay open after an accepted value, with this status line, for the next entry.</param>
    /// <param name="doneLabel">The dismiss label once a continuing sheet has accepted something.</param>
    /// <returns>The entered text, or <see langword="null"/> if cancelled.</returns>
    public static async Task<string?> ShowTextInputSheetAsync(
        this IDialogService dialogs,
        string title,
        string label,
        string confirmLabel,
        string cancelLabel,
        string? initial = null,
        string? hint = null,
        Func<string, Task<string?>>? submitting = null,
        Func<string, string>? continueWith = null,
        string? doneLabel = null)
    {
        var parameters = new DialogParameters<TextInputSheet>
        {
            { x => x.Label, label },
            { x => x.ConfirmLabel, confirmLabel },
            { x => x.CancelLabel, cancelLabel },
            { x => x.Initial, initial },
            { x => x.Hint, hint },
            { x => x.Submitting, submitting },
            { x => x.Continue, continueWith },
            { x => x.DoneLabel, doneLabel },
        };

        // Centered, matching the create-group sheet (NewGroupDialog). A top-anchored sheet sat under the
        // phone status bar / notch (MudBlazor pads top dialogs a flat 32px, less than the iOS safe-area
        // inset); centering clears both the status bar and the soft keyboard, which the browser scrolls the
        // focused field above — the create-group sheet also auto-focuses a field and reads correctly this
        // way (user-reported on iOS + Android). The confirm-only ConfirmSheet stays a bottom sheet.
        var options = new DialogOptions
        {
            Position = DialogPosition.Center,
            FullWidth = true,
            CloseOnEscapeKey = true,
        };

        var reference = await dialogs.ShowAsync<TextInputSheet>(title, parameters, options);
        var result = await reference.Result;
        return result is { Canceled: false, Data: string text } ? text : null;
    }
}
