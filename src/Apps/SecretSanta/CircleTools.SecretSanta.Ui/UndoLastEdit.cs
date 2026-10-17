using Microsoft.Extensions.Localization;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The way back from an autosaved text edit (2026-10-17 decision): text fields save themselves with no Save
/// button (Q29), so leaving a field whose text changed shows a short "Change saved · Undo" that restores the
/// text the field had before that edit. While still typing, the platform's own undo applies.
/// </summary>
/// <remarks>
/// One offer at a time, under one key: moving through several fields replaces it, so Undo always takes back
/// the latest edit. The undo runs the editor's ordinary save, so it works after the page was left too.
/// </remarks>
public static class UndoLastEdit
{
    /// <summary>The snackbar key every undo offer shares, so a new one replaces the previous.</summary>
    public const string SnackbarKey = "undo-last-edit";

    private const int VisibleMilliseconds = 6000;

    /// <summary>Offers to undo the edit just saved.</summary>
    /// <param name="snackbar">The snackbar service.</param>
    /// <param name="localizer">The app strings.</param>
    /// <param name="undo">Restores and saves the previous text.</param>
    public static void Offer(ISnackbar snackbar, IStringLocalizer<AppStrings> localizer, Func<Task> undo)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(undo);

        snackbar.RemoveByKey(SnackbarKey);
        snackbar.Add(localizer["ChangeSaved"], Severity.Normal, options =>
        {
            options.Action = localizer["Undo"];
            options.ActionColor = Color.Primary;
            options.VisibleStateDuration = VisibleMilliseconds;
            options.CloseAfterNavigation = false;
            options.OnClick = async _ => await undo();
        }, SnackbarKey);
    }
}
