// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using CircleTools.SecretSanta.Contracts;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Shared;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Builds the <see cref="CalendarEvent"/> for an occasion's "Add to calendar" action (MNF-6, FR-044),
/// shared by the organizer and linked-member surfaces so the entry — title, all-day date, location,
/// notes + budget range, and the reopen deep link — is composed one way. The reveal page composes its
/// own (a generic title and the reveal link as the reopen URL), since a raw-link holder need not have the
/// group.
/// </summary>
public static class CalendarEventFactory
{
    /// <summary>
    /// Builds the entry for one occasion, or <see langword="null"/> when it has no date (no date → no
    /// calendar entry, FR-044).
    /// </summary>
    /// <param name="groupId">The owning group.</param>
    /// <param name="eventId">The occasion.</param>
    /// <param name="title">The event name (already defaulted for an unnamed event).</param>
    /// <param name="date">The occasion date, or <see langword="null"/> when unset.</param>
    /// <param name="location">The optional location.</param>
    /// <param name="notes">The optional free-text notes.</param>
    /// <param name="budget">The optional budget, appended to the notes as a range.</param>
    /// <param name="budgetLabel">The localized "Budget" label for the notes line.</param>
    /// <param name="baseUrl">The reveal/app base URL; the reopen deep link is built from it.</param>
    /// <returns>The entry, or <see langword="null"/> when there is no date.</returns>
    public static CalendarEvent? ForOccasion(
        Guid groupId,
        Guid eventId,
        string title,
        DateTimeOffset? date,
        string? location,
        string? notes,
        BudgetDto? budget,
        string budgetLabel,
        string? baseUrl)
    {
        if (date is not { } value)
        {
            return null;
        }

        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}/groups/{groupId}/events/{eventId}";

        return new CalendarEvent(
            Uid: $"secretsanta-{groupId}-{eventId}@circletools.ch",
            Title: title,
            Date: DateOnly.FromDateTime(value.Date),
            Location: Blank(location),
            Notes: ComposeNotes(notes, budget, budgetLabel),
            Url: url);
    }

    /// <summary>Notes first, then the budget range on its own labeled line — the calendar's DESCRIPTION body.</summary>
    /// <param name="notes">The free-text notes, or <see langword="null"/>.</param>
    /// <param name="budget">The budget, or <see langword="null"/>.</param>
    /// <param name="budgetLabel">The localized "Budget" label.</param>
    /// <returns>The composed notes, or <see langword="null"/> when there is nothing to show.</returns>
    public static string? ComposeNotes(string? notes, BudgetDto? budget, string budgetLabel)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(notes))
        {
            parts.Add(notes.Trim());
        }

        if (budget is { } b)
        {
            parts.Add($"{LabelText.WithColon(budgetLabel, CultureInfo.CurrentUICulture)} {BudgetDisplay.Format(b.MinAmount, b.Amount, b.Currency)}");
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
