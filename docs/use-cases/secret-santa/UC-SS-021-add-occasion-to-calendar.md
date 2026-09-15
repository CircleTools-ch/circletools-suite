---
id: UC-SS-021
title: Add an occasion to my phone calendar
app: Secret-Santa
actors: [Organizer, Participant]
status: Draft
priority: Could
related: [FR-044, FEAT-014, TP-014, ADR-0028, ADR-0053, UC-SS-006, UC-SS-011, UC-SS-013]
---

# UC-SS-021 — Add an occasion to my phone calendar

## Summary / goal

Anyone looking at an [Event](../../glossary/event.md) that has a date — the
Organizer planning it, or a giver who just opened their
[reveal link](../../glossary/reveal-link.md) — wants the occasion in their own calendar so they do not
forget when (and where) the exchange happens. One tap should hand the occasion's
[event details](../../glossary/event-details.md) to the phone's calendar, pre-filled, for the person to
review and save.

## Primary actor & stakeholders

- **Primary actor:** the Organizer (on the event's details/hub screens) **or** a Participant/giver (on
  the reveal or my-assignments screen).
- **Stakeholders & interests:**
  - The person adding it — wants the date on their calendar with no retyping, and wants to *see* what
    is being added before it lands (nothing written silently).
  - Everyone in the exchange — a reminder that reaches every giver, not just the organizer, is what
    stops a forgotten hand-over.

## Preconditions

- The Event exists and its [event details](../../glossary/event-details.md) carry a **date** (location
  and notes are optional). Without a date there is nothing to schedule.
- The person is on a screen that shows the occasion: the organizer on the event **Details** section or
  the event hub; a giver on the **reveal** page ([UC-SS-006](UC-SS-006-open-reveal-link.md)) or their
  in-app **my-assignments** ([UC-SS-011](UC-SS-011-see-my-assignment.md)).
- No account, sign-in, or connection is required — the calendar entry is built on the device from data
  already present ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Postconditions

- **Success:** the platform's own calendar UI opens **pre-filled** with the occasion (title, date,
  location, notes); the person confirms and it is saved to the calendar they choose. Nothing in
  Secret-Santa changes.
- **Failure:** no calendar entry is created; the person stays where they were, told nothing was added.

## Trigger

The person taps **Add to calendar** on an occasion that has a date.

## Main success scenario

1. The person views an occasion whose date is set; an **Add to calendar** action is offered.
2. The person taps it.
3. The app builds the occasion as a standard calendar entry — **all-day** on the event's date, titled
   with the event name, carrying the location, a note that combines the free-text notes and the
   [budget](../../glossary/budget.md) range (e.g. *"10–30 CHF"*), and a **link back into the app at the
   occasion** so the entry can reopen the event later.
4. The device shows its **own** calendar UI, pre-filled:
   - on Android, the calendar's *new-event editor*;
   - on iOS, the Calendar *add-event preview*;
   - in a browser, the downloaded/opened calendar file handed to the OS calendar.
5. The person reviews the pre-filled details, picks a calendar if prompted, and **saves**.
6. Later, tapping the calendar entry's link **reopens the app at that occasion** — on Android the installed
   app opens (offline, via the App Link), on the web the app route opens in the browser. This works the same
   for a local, shared-by-link, or account group, because the device already holds that group.

## Extensions / alternate flows

- **1a. The occasion has no date** → **Add to calendar** is not offered (a calendar entry needs a date).
- **3a. Location and/or notes are empty** → they are simply omitted from the entry; the dated title is
  enough.
- **4a. The giver is on the account-free reveal page** → the entry is still built entirely client-side
  (the reveal page is self-contained, [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)),
  so add-to-calendar works with no account and no network.
- **5a. The person cancels the calendar UI** → nothing is saved; a dismissed sheet is a normal outcome,
  not an error.

## Exceptions

- **The platform has no reachable calendar app** (rare desktop browser) → the calendar file is offered
  for download regardless; the OS decides what opens it. The action never blocks the page.

## Non-functional notes

- **Offline/local-first:** builds entirely on the device from local data; no account, sign-in, or
  connection ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Privacy:** the entry carries only **non-secret** occasion facts — name, date, location, notes,
  budget range, and a deep link that is a *location* (opaque group/event GUIDs), not a capability: opening
  it does nothing unless you already hold the group. It **never** carries who drew whom
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)); the giftee is not part
  of an occasion's calendar entry even on the reveal screen.
- **Transparency:** the calendar entry is created only through the platform's **own** UI, which the
  person sees and confirms — no silent write, and no calendar-write permission is requested.
- **Localization:** the **Add to calendar** label and any hint are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); the event name and
  notes carry through as the person typed them, and the budget range formats for the active culture
  ([ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md)).

## Open questions

- None open on the deep link — it is **in v1 scope**, reusing the App Link machinery
  ([ADR-0053](../../adr/0053-backend-free-first-mobile-release.md)). It inherits the same origin/fingerprint
  behaviour reveal links already ship with (build-time origin; Android shows the chooser until
  `assetlinks.json` carries the real Play-signing fingerprint) — a known, shared trait, not a new
  limitation. iOS deep-link wiring waits with the iOS head (MNF-10).

## Traceability

- Requirements: [FR-044](../../requirements/functional/FR-044-add-event-to-device-calendar.md)
- Feature spec: [FEAT-014](../../features/FEAT-014-add-event-to-calendar.md) /
  [TP-014](../../features/TP-014-add-event-to-calendar.md)
- Related use cases: [UC-SS-013](UC-SS-013-manage-events.md) (the occasion whose details this reads),
  [UC-SS-006](UC-SS-006-open-reveal-link.md) and [UC-SS-011](UC-SS-011-see-my-assignment.md) (the
  giver-facing surfaces that also offer it)
- Governing ADR: [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) (the platform-seam
  amendment for the calendar launcher); [ADR-0053](../../adr/0053-backend-free-first-mobile-release.md)
  (the App Link machinery the in-entry deep link reuses)
- Glossary: [Event](../../glossary/event.md), [Event details](../../glossary/event-details.md),
  [Budget](../../glossary/budget.md), [Reveal link](../../glossary/reveal-link.md)
- Source: MNF-6 in the [backlog](../../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02),
  from the [mobile brainstorm (2026-09-01)](../../brainstorming/2026-09-01-secret-santa-mobile.md)
