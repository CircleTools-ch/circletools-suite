using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The answer to the sign-in reconciliation prompt (ADR-0044 amendment 2026-10-16): which groups to import into
/// the account and which to keep on this device. There is no discard.
/// </summary>
/// <param name="Import">The ticked groups, to import.</param>
/// <param name="KeepLocal">The unticked groups, kept on this device and not asked about again.</param>
public sealed record ReconcileAnswer(IReadOnlyList<Guid> Import, IReadOnlyList<Guid> KeepLocal);

/// <summary>Shows the sign-in reconciliation prompt as a bottom sheet that must be answered.</summary>
public static class ReconcileSheetExtensions
{
    /// <summary>Asks what to do with the groups made while signed out.</summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="groups">The groups, named in the sheet.</param>
    /// <param name="cancellationToken">Closes the sheet unanswered (the prompt's owner went away).</param>
    /// <returns>The answer, or <see langword="null"/> for "Later" or no answer — nothing decided.</returns>
    public static async Task<ReconcileAnswer?> ShowReconcileSheetAsync(
        this IDialogService dialogs,
        IReadOnlyList<ClaimableGroup> groups,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DialogParameters<ReconcileSheet>
        {
            { x => x.Groups, groups },
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
        return result is { Canceled: false, Data: ReconcileAnswer answer } ? answer : null;
    }
}
