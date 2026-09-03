---
id: UC-SS-015
title: Set a minimum as well as a maximum gift budget
app: Secret-Santa
actors: [Organizer]
status: Reviewed
priority: Could
related: [FR-036, UC-SS-001, UC-SS-013, FR-001, NFR-001, NFR-003]
---

# UC-SS-015 — Set a minimum as well as a maximum gift budget

## Summary / goal

An organizer wants to guide spending with a **range**, not just a ceiling: "spend between 10 and 30
francs". Today an [Event](../../glossary/event.md)'s [Budget](../../glossary/budget.md) is a single
amount — a **maximum**. This use case lets the organizer add an optional **minimum** so a giver knows
the exchange expects a gift worth *at least* a floor as well as *at most* the limit, and so a bought
"cheap-out" gift can't undercut everyone else's.

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members / givers (want to know the expected spend, both ends, so their gift fits
  in); recipients (a floor keeps the exchange fair).

## Preconditions

- A group with at least one event exists; the organizer is on that event's details screen.

## Postconditions

- **Success:** the event's budget carries a maximum and, optionally, a minimum (`0 ≤ min ≤ max`, one
  currency). The range shows on every reveal link and on a linked member's own assignment.
- **Failure:** nothing is saved; the organizer is told why (e.g. the minimum exceeds the maximum).

## Trigger

The organizer edits an event's budget on the event-details screen.

## Main success scenario

1. Organizer opens an event's **Details** screen.
2. Organizer enters a **Maximum** amount and a **Currency** (as today) and, optionally, a **Minimum**.
3. Organizer saves.
4. The app validates (`0 ≤ minimum ≤ maximum`, valid currency) and stores the budget.
5. When the draw is shared, each [Reveal link](../../glossary/reveal-link.md) shows the budget as a
   **range** ("10–30 CHF") when a minimum is set, or just the maximum ("30 CHF") when it is not.

## Extensions / alternate flows

- **2a.** Organizer leaves the **Minimum** blank → the budget behaves exactly as today (a bare
  maximum); no range is shown.
- **2b.** Organizer clears the budget (no maximum) → the budget is removed, minimum and all; nothing is
  shown on the reveal.
- **4a.** The minimum is greater than the maximum → the app rejects the save and shows an inline error;
  nothing is stored.
- **4b.** The minimum is zero or negative → treated as "no minimum" (a bare maximum), not an error.

## Exceptions

- **Local storage unavailable** → the system reports the error and does not claim the budget was saved.

## Non-functional notes

- **Offline/local-first:** setting the budget is a local operation on the one `Group` document; nothing
  new crosses to the server, and the group stays one synced unit when signed in
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** the *Minimum* / *Maximum* labels, the min-exceeds-max error, and the range display
  are in DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); amounts format for
  the active region ([ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md)).
- **Privacy/link size:** the minimum rides in the existing self-contained reveal payload
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)); it is a single optional number and
  is **omitted from the encoded link entirely when unset**, so max-only links are unchanged in size.

## Open questions

- Is a min-only budget (a floor with no ceiling) ever wanted? Not offered in v1 — the maximum is the
  anchor of "a budget". Revisit if asked.

## Traceability

- Requirement: [FR-036](../../requirements/functional/FR-036-budget-minimum-and-maximum.md)
- Builds on: [UC-SS-001](UC-SS-001-create-group.md) (budget first appears),
  [UC-SS-013](UC-SS-013-manage-events.md) (per-event budget editing)
- Governing ADR: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal payload — the
  minimum is an additive, version-compatible field) — no new ADR needed
- Feature spec: [FEAT-011](../../features/FEAT-011-budget-minimum-and-maximum.md)
- Glossary: [Budget](../../glossary/budget.md), [Reveal link](../../glossary/reveal-link.md)
