---
id: UC-SS-013
title: Manage multiple events (occasions) in a group
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Should
related: [FR-034, FR-004, FR-005, NFR-001, NFR-003]
---

# UC-SS-013 — Manage multiple events (occasions) in a group

## Summary / goal

An organizer reuses one [Group](../../glossary/group.md)'s shared roster across several
[Events](../../glossary/event.md) — occasions like Christmas and a birthday — each with its own
[Draw](../../glossary/draw.md), [Wishlists](../../glossary/wishlist.md),
[Budget](../../glossary/budget.md) and [Event details](../../glossary/event-details.md), without
re-entering the people each time.

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (want each occasion to have the right list and a fair, independent draw).

## Preconditions

- A group exists with its shared roster (and, optionally, exclusions).

## Postconditions

- **Success:** the group has the intended set of events; each carries its own draw, wishlists, budget
  and date, independent of the others.
- **Failure:** nothing is persisted; the organizer sees why (e.g. the last event cannot be removed).

## Trigger

The organizer opens a group and works with its **Events** list.

## Main success scenario

1. Organizer opens a group; the group shows its **Events** (a new group has one default event).
2. Organizer adds an event and names it (e.g. "Christmas 2026").
3. Organizer opens the event and sets its budget and date, and each member's wishlist for it.
4. Organizer runs the draw **for that event**; the result is that event's own draw.
5. Organizer shares that event's reveal links.
6. Organizer repeats 2–5 for another occasion, reusing the same roster.

## Extensions / alternate flows

- **2a.** Organizer leaves an event unnamed → the UI shows a generic label for it (fine for a group
  with a single occasion).
- **3a./4a.** Adding or removing a member on the **shared roster** voids the draw of **every** event;
  the organizer is warned and re-draws each event when ready.
- **4b.** Re-drawing one event does not affect any other event's draw.
- **7a. (remove)** Organizer removes an event → its draw and wishlists are discarded; **the last
  remaining event cannot be removed** (a group always has at least one).

## Exceptions

- **Local storage unavailable** → the system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** the whole flow works with no account; a group (with all its events) is one
  synced document when signed in ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** all labels in DE/EN/FR; an unnamed event shows a localized default label
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Traceability

- Requirement: [FR-034](../../requirements/functional/FR-034-multiple-events-per-group.md); related
  [FR-004](../../requirements/functional/FR-004-run-fair-draw.md),
  [FR-005](../../requirements/functional/FR-005-generate-reveal-links.md)
- Governing ADR: [ADR-0042](../../adr/0042-events-as-first-class-entities.md)
- Glossary: [Event](../../glossary/event.md), [Group](../../glossary/group.md),
  [Draw](../../glossary/draw.md), [Wishlist](../../glossary/wishlist.md)
