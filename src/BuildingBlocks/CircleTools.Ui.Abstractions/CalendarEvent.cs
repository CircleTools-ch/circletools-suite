// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// A single occasion to hand to the platform's calendar UI (MNF-6, FR-044). Head-agnostic: the same
/// value is turned into an iCalendar (<c>.ics</c>) entry on the web and iOS, and into an
/// <c>ACTION_INSERT</c> intent on Android. It is an <strong>all-day</strong> event on <see cref="Date"/>
/// (Secret-Santa occasions are date-only), and it never carries an assignment — who drew whom is not an
/// occasion fact (NFR-002).
/// </summary>
/// <param name="Uid">
/// A stable identifier for the entry, so a calendar can recognise a re-add of the same occasion rather
/// than duplicate it (e.g. <c>secretsanta-{groupId}-{eventId}@circletools.ch</c>).
/// </param>
/// <param name="Title">The entry summary — the event name, or a generic label where none is known.</param>
/// <param name="Date">The occasion's calendar day (local). Rendered as an all-day entry.</param>
/// <param name="Location">The optional location; omitted from the entry when null/empty.</param>
/// <param name="Notes">The optional free-text notes (with the budget range appended by the caller); omitted when null/empty.</param>
/// <param name="Url">
/// An optional link that reopens the occasion — the app's event route on the organizer/linked surfaces,
/// or the reveal link itself on the reveal page. Placed in the entry's URL field and its notes. Carries
/// no secret (an app route is a location, not a capability).
/// </param>
public sealed record CalendarEvent(
    string Uid,
    string Title,
    DateOnly Date,
    string? Location = null,
    string? Notes = null,
    string? Url = null);
