using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Shows a <see cref="QrDialog"/> presenting one reveal link as a scannable QR code (MNF-5).
/// </summary>
public static class QrDialogExtensions
{
    /// <summary>
    /// Presents the QR code for a reveal link in a centered dialog so it can be scanned off the screen.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="label">The recipient's name (dialog title and image alt text).</param>
    /// <param name="url">The reveal-link URL to encode.</param>
    /// <param name="hint">A one-line instruction shown under the code.</param>
    /// <param name="closeLabel">Label for the dismiss button.</param>
    public static Task<IDialogReference> ShowQrDialogAsync(
        this IDialogService dialogs,
        string label,
        string url,
        string hint,
        string closeLabel)
    {
        var parameters = new DialogParameters<QrDialog>
        {
            { x => x.Label, label },
            { x => x.Url, url },
            { x => x.Hint, hint },
            { x => x.CloseLabel, closeLabel },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.Center,
            // The default (Small, up to 600px) is wider than a phone, so the dialog ran edge to edge; this keeps the
            // 32px margin the other dialogs have (Pixel pass, 2026-10-28).
            MaxWidth = MaxWidth.False,
            CloseOnEscapeKey = true,
            // No corner X: the dialog's own Close button is the one way out (two close controls side by
            // side was redundant - UX review P5). Tapping outside / Back also closes it.
        };

        return dialogs.ShowAsync<QrDialog>(title: null, parameters, options);
    }
}
