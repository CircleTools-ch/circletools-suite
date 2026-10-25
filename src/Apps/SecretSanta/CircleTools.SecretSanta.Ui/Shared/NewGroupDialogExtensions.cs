using CircleTools.SecretSanta.LocalStore.Online;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>The name and chosen place a <see cref="Shared.NewGroupDialog"/> returns.</summary>
/// <param name="Name">The trimmed group name.</param>
/// <param name="Place">Where the group is kept (ADR-0086).</param>
/// <param name="FromCircle">The person chose to take the members from a circle instead (T1).</param>
public sealed record NewGroupResult(string Name, GroupPlace Place, bool FromCircle = false);

/// <summary>Shows the phone "New group" sheet (ADR-0073): name + place.</summary>
public static class NewGroupDialogExtensions
{
    /// <summary>
    /// Presents the create-group sheet, pre-selecting the current default storage mode.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="defaultPlace">The default place, pre-selected and shown.</param>
    /// <param name="accountEnabled">Whether someone is signed in (it changes what "online" means).</param>
    /// <param name="explainStorage">Show the storage options expanded with their explanations (the first group a device creates).</param>
    /// <param name="offerCircle">Offer "Take members from a circle" (the account has a circle with 2+ people).</param>
    /// <param name="initialName">A prefilled name.</param>
    /// <param name="fromTemplate">Starting from another group (Q34): says that members and exclusions come along.</param>
    /// <returns>The name and chosen mode, or <see langword="null"/> if cancelled.</returns>
    public static async Task<NewGroupResult?> ShowNewGroupDialogAsync(
        this IDialogService dialogs,
        GroupPlace defaultPlace,
        bool accountEnabled,
        bool explainStorage = false,
        bool offerCircle = false,
        string? initialName = null,
        bool fromTemplate = false)
    {
        var parameters = new DialogParameters<Shared.NewGroupDialog>
        {
            { x => x.Place, defaultPlace },
            { x => x.AccountEnabled, accountEnabled },
            { x => x.ExplainStorage, explainStorage },
            { x => x.OfferCircle, offerCircle },
            { x => x.InitialName, initialName },
            { x => x.FromTemplate, fromTemplate },
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
