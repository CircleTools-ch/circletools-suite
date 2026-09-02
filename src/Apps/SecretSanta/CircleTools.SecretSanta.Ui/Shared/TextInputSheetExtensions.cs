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
    /// <returns>The entered text, or <see langword="null"/> if cancelled.</returns>
    public static async Task<string?> ShowTextInputSheetAsync(
        this IDialogService dialogs,
        string title,
        string label,
        string confirmLabel,
        string cancelLabel)
    {
        var parameters = new DialogParameters<TextInputSheet>
        {
            { x => x.Label, label },
            { x => x.ConfirmLabel, confirmLabel },
            { x => x.CancelLabel, cancelLabel },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.BottomCenter,
            FullWidth = true,
            CloseOnEscapeKey = true,
        };

        var reference = await dialogs.ShowAsync<TextInputSheet>(title, parameters, options);
        var result = await reference.Result;
        return result is { Canceled: false, Data: string text } ? text : null;
    }
}
