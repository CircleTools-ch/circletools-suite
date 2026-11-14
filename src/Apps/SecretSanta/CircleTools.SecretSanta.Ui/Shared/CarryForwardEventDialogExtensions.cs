// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Ui.Shared;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>The organizer's choice from the carry-forward add-event dialog (FR-035).</summary>
/// <param name="Name">The new occasion's name (trimmed, non-empty).</param>
/// <param name="SourceEventId">
/// The event to carry forward from, or <see langword="null"/> to start blank.
/// </param>
/// <param name="Date">The occasion's date when given right away (Q22), or <see langword="null"/>.</param>
public sealed record CarryForwardEventChoice(string Name, Guid? SourceEventId, DateOnly? Date = null);

/// <summary>
/// Shows the <see cref="CarryForwardEventDialog"/> — add a new occasion, optionally carrying forward
/// wishlists, budget and the no-repeat draw baseline from a previous one (FR-035 / UC-SS-014).
/// </summary>
public static class CarryForwardEventDialogExtensions
{
    /// <summary>
    /// Presents the add-with-carry-forward dialog and returns the organizer's choice, or
    /// <see langword="null"/> if cancelled.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="events">The group's existing events to offer as carry-forward sources.</param>
    /// <param name="initialName">The pre-filled default name for the new occasion.</param>
    /// <returns>The choice (name + optional source event), or <see langword="null"/> if cancelled.</returns>
    public static async Task<CarryForwardEventChoice?> ShowCarryForwardEventDialogAsync(
        this IDialogService dialogs,
        string title,
        IReadOnlyList<EventSummaryDto> events,
        string? initialName = null)
    {
        var parameters = new DialogParameters<CarryForwardEventDialog>
        {
            { x => x.Events, events },
            { x => x.InitialName, initialName },
        };

        // Centered, matching the add-member (TextInputSheet) and create-group (NewGroupDialog) sheets: the
        // name field raises the soft keyboard, and a top-anchored sheet sat under the phone status bar /
        // notch, while a bottom one would be covered by the keyboard. Centering clears both — the browser
        // scrolls the focused field above the keyboard.
        var options = new DialogOptions
        {
            Position = DialogPosition.Center,
            FullWidth = true,
            CloseOnEscapeKey = true,
        };

        var reference = await dialogs.ShowAsync<CarryForwardEventDialog>(title, parameters, options);
        var result = await reference.Result;
        return result is { Canceled: false, Data: CarryForwardEventChoice choice } ? choice : null;
    }
}
