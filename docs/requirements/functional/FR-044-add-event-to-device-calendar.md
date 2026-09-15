---
id: FR-044
title: Add an event to the device calendar
type: functional
status: Implemented
priority: Could
source: UC-SS-021
verification: test
related: [UC-SS-021, FEAT-014, TP-014, ADR-0028, ADR-0053, ADR-0056, NFR-001, NFR-002, NFR-003]
---

# FR-044 — Add an event to the device calendar

## Description

For any [Event](../../glossary/event.md) whose [event details](../../glossary/event-details.md) carry a
**date**, the app offers an **Add to calendar** action that hands the occasion to the platform's own
calendar UI, **pre-filled**, for the person to review and save. It is available to the organizer (on the
event details/hub) **and** to a giver on the [reveal link](../../glossary/reveal-link.md) and in-app
my-assignments surfaces. The entry is built entirely on the device — no account, sign-in, or connection
([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

The occasion is expressed as a standard **all-day** calendar entry on the event's date (the date is
date-only, so there is no start/end time), with:

- **Title** = the event name (e.g. *"Christmas 2026"*) on the organizer/my-assignments surfaces; on the
  reveal page, a generic title (the app name), since the reveal payload carries no event name;
- **Location** = the event location, if any;
- **Notes** = the free-text notes, if any, followed by the [budget](../../glossary/budget.md) range
  (e.g. *"10–30 CHF"*, omitted when no budget is set), formatted for the active culture
  ([ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md)), followed by the deep link
  below;
- **Deep link** = a link back into the app at the occasion, placed in the iCalendar `URL` property **and**
  repeated in the notes (some calendars do not surface `URL`), built from the **same configured base URL
  reveal links use**. The exact link is **per surface**: the organizer and my-assignments surfaces use the
  app's own event route `{origin}/groups/{groupId}/events/{eventId}` (the `EventHub` route), which works for
  **all three storage modes** (local, shared-by-link, account) because the device that added the entry
  already holds that group; the **reveal page** uses the **reveal link itself**, because a raw-link holder
  need not have the group and their own link is what reopens their view (offline, ADR-0014). On Android the
  event route reuses the existing App Link / deep-link machinery
  ([ADR-0053](../../adr/0053-backend-free-first-mobile-release.md)) and works offline; on the web it opens
  the app route in the browser; on iOS it falls back to the browser/PWA (MNF-10). The event-route link
  carries **no secret** — only opaque GUIDs, and opening it does nothing unless you already hold the group,
  so it is a *location*, not a capability; the reveal-page link is the recipient's **own** reveal link placed
  in their **own** calendar entry — the secret they already hold, no new exposure. Either way the entry never
  contains an assignment, so it raises no
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md) concern.

The entry **never** contains an [assignment](../../glossary/assignment.md) — who drew whom is not an
occasion fact and must not leave through the calendar
([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). The calendar entry is
created only through the platform's own confirm UI: **no silent write**, and no calendar-write permission
is requested.

## Rationale

The date/location/notes are already captured per occasion; retyping them into a personal calendar is
pure friction, and the cross-market review found givers wanting to be reminded, not just organizers
(MNF-6, from the [mobile brainstorm](../../brainstorming/2026-09-01-secret-santa-mobile.md)). Handing the
occasion to the phone's calendar in one tap is the smallest thing that turns "there is a date somewhere
in the app" into "it is on my calendar." Doing it through the platform's own pre-filled UI keeps the user
in control, needs no calendar permission, and — with the calendar file built client-side — works even on
the account-free reveal page.

## Acceptance criteria

```gherkin
Given an event whose details include a date
When I open the event's Details section (organizer) or my reveal / my-assignments (giver)
Then an "Add to calendar" action is shown

Given an event whose details have no date
When I view that event on any surface
Then no "Add to calendar" action is shown

Given an event dated 24 December 2026 named "Christmas 2026" with a location, notes and a 10–30 CHF budget
When I tap "Add to calendar"
Then the platform's own calendar UI opens pre-filled with an all-day entry on 2026-12-24
And its title is "Christmas 2026", its location is the event location
And its notes contain the free-text notes and the budget range "10–30 CHF"

Given an event with group id "G" and event id "E"
When I tap "Add to calendar" from the event hub or my-assignments
Then the calendar entry's URL is "{origin}/groups/G/events/E" using the same base URL as reveal links
And that link also appears in the entry's notes

Given I am on the reveal page for my link
When I tap "Add to calendar"
Then the calendar entry's URL is my reveal link (which reopens my assignment offline)

Given the installed Android app and a calendar entry carrying the app's event deep link
When I tap that link in my calendar
Then the app opens at that occasion (offline), for a local, shared-by-link, or account group alike

Given the pre-filled calendar UI is shown
When I do not confirm it (I dismiss/cancel)
Then nothing is saved to the calendar and nothing in the app changes

Given any event
When its calendar entry is built
Then the entry contains no assignment (no giver→recipient information)

Given I am on the account-free reveal page with no connection
When I tap "Add to calendar"
Then the entry is built on the device and the calendar UI still opens
```

## Dependencies & related

- Derived from: [UC-SS-021](../../use-cases/secret-santa/UC-SS-021-add-occasion-to-calendar.md)
- Related: [ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md) (budget/date
  formatting), [NFR-001](../non-functional/NFR-001-local-first-offline.md) (offline, on-device),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (no assignment leaks into the entry),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md) (DE/EN/FR strings)
- Governing ADR: [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) — the calendar-launcher
  platform seam (amendment); [ADR-0053](../../adr/0053-backend-free-first-mobile-release.md) — the App
  Link / deep-link machinery the in-entry link reuses (extended, no new ADR)
- Implemented by: [FEAT-014](../../features/FEAT-014-add-event-to-calendar.md)
- Glossary: [Event](../../glossary/event.md), [Event details](../../glossary/event-details.md),
  [Budget](../../glossary/budget.md), [Reveal link](../../glossary/reveal-link.md)
