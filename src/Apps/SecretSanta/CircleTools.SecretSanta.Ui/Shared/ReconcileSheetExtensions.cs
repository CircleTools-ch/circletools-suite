using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>The answers to the sign-in reconciliation prompt (ADR-0044).</summary>
public enum ReconcileChoice
{
    /// <summary>Claim the groups into the account.</summary>
    Import,

    /// <summary>Keep the groups on this device only, and don't ask about them again.</summary>
    KeepLocal,

    /// <summary>Delete the groups.</summary>
    Discard,
}

/// <summary>Shows a <see cref="ReconcileSheet"/> and resolves to the user's answer (ADR-0044 amendment 2026-10-15).</summary>
public static class ReconcileSheetExtensions
{
    /// <summary>
    /// Presents the reconciliation prompt as a bottom sheet that only its buttons close.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="title">The title.</param>
    /// <param name="message">The question.</param>
    /// <param name="importLabel">Label for Import.</param>
    /// <param name="keepLocalLabel">Label for Keep on this device.</param>
    /// <param name="discardLabel">Label for Discard.</param>
    /// <param name="cancellationToken">
    /// Closes the sheet unanswered — when the asking component goes away. Open dialogs outlive a component
    /// tree rebuild (the language switch), so a stale sheet must be closed by whoever opened it.
    /// </param>
    /// <returns>The answer, or <see langword="null"/> when the sheet closed without one — which decides nothing.</returns>
    public static async Task<ReconcileChoice?> ShowReconcileSheetAsync(
        this IDialogService dialogs,
        string title,
        string message,
        string importLabel,
        string keepLocalLabel,
        string discardLabel,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DialogParameters<ReconcileSheet>
        {
            { x => x.Title, title },
            { x => x.Message, message },
            { x => x.ImportLabel, importLabel },
            { x => x.KeepLocalLabel, keepLocalLabel },
            { x => x.DiscardLabel, discardLabel },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.BottomCenter,
            FullWidth = true,
            BackdropClick = false,
            CloseOnEscapeKey = false,
            CloseButton = false,
        };

        var reference = await dialogs.ShowAsync<ReconcileSheet>(title: null, parameters, options);
        await using var closeOnCancel = cancellationToken.Register(() => reference.Close());
        var result = await reference.Result;
        return result is { Canceled: false, Data: ReconcileChoice choice } ? choice : null;
    }
}
