using CircleTools.SecretSanta.LocalStore;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Shows the one-time storage-mode onboarding dialog (FR-048, ADR-0071).
/// </summary>
public static class OnboardingStorageDialogExtensions
{
    /// <summary>
    /// Presents the storage onboarding, letting the user set the default storage mode for new groups.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="current">The current default, pre-selected in the picker.</param>
    /// <param name="signedIn">Whether a session is live. "My account" is always selectable; when signed
    /// out and it is chosen, the caller routes the user to sign in / register.</param>
    /// <returns>The chosen default mode, or <see langword="null"/> if the user chose "Not now" (keep the
    /// current default). The caller marks onboarding as shown in either case.</returns>
    public static async Task<StorageMode?> ShowStorageOnboardingAsync(
        this IDialogService dialogs,
        StorageMode current,
        bool signedIn)
    {
        var parameters = new DialogParameters<Shared.OnboardingStorageDialog>
        {
            { x => x.Value, current },
            { x => x.SignedIn, signedIn },
        };

        var options = new DialogOptions
        {
            Position = DialogPosition.Center,
            CloseOnEscapeKey = true,
            BackdropClick = false,
        };

        var dialog = await dialogs.ShowAsync<Shared.OnboardingStorageDialog>(title: null, parameters, options);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: StorageMode mode } ? mode : null;
    }
}
