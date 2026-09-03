---
id: UC-SS-014
title: Create a new occasion carrying forward from a previous one
app: Secret-Santa
actors: [Organizer]
status: Reviewed
priority: Should
related: [FR-035, UC-SS-013, FR-002, FR-004, FR-034, NFR-001, NFR-003]
---

# UC-SS-014 — Create a new occasion carrying forward from a previous one

## Summary / goal

When an organizer runs the **same** gift exchange again — Christmas this year after Christmas last
year — they want the new [Event](../../glossary/event.md) to start from the previous one instead of a
blank slate: the members' [Wishlists](../../glossary/wishlist.md) already filled in, the
[Budget](../../glossary/budget.md) already set, and a [Draw](../../glossary/draw.md) that **won't
reproduce last year's pairings**. This is [Carry-forward](../../glossary/carry-forward.md).

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (want last year's wishlist as a starting point they can tweak, not to
  re-dictate it; want a genuinely different giftee this year). The organizer of a family exchange
  especially wants **not to re-type the children's lists** every year.

## Preconditions

- A group exists and already has **at least one** event (the source occasion). Its wishlists/budget
  may or may not be filled; a carried-forward blank simply seeds blanks.

## Postconditions

- **Success:** the group has a new, **undrawn** event whose per-member wishlists and budget are
  **copied from the chosen source event** (for members still on the shared roster) and whose draw
  history is seeded with the source event's draw, so its first draw avoids repeating that pairing.
  Everything copied is **editable** afterwards; the source event is unchanged.
- **Failure:** no event is created; the organizer sees why; the source event is untouched.

## Trigger

The organizer adds an event to a group that already has one, and chooses to carry forward from an
existing occasion.

## Main success scenario

1. Organizer adds an event to a group that already has at least one event.
2. The app offers to **carry forward from** an existing event (defaulting to the most recent one) —
   or to start blank.
3. Organizer accepts (or picks a different source event) and confirms the name (pre-filled, e.g.
   "Christmas 2027").
4. The app creates the new event and copies from the source: **each current member's wishlist**, the
   **budget**, and records the source event's draw as the new event's **draw-history baseline**.
   [Exclusions](../../glossary/exclusion.md) already apply — they are shared at the group level
   ([FR-034](../../requirements/functional/FR-034-multiple-events-per-group.md)) — so nothing is
   copied for them.
5. Organizer reviews and edits any wishlist/budget, then runs the draw for the new event; the draw
   **honours the carried-forward baseline** and does not reproduce the source event's pairings
   (draw rule (c), [UC-SS-004](UC-SS-004-run-draw.md)).
6. Organizer shares the new event's reveal links as usual.

## Extensions / alternate flows

- **2a.** The group has only the one (default) event and it is still empty → carry-forward is offered
  but copies nothing meaningful; starting blank is equivalent.
- **3a.** Organizer chooses **start blank** → behaves exactly as today's add-event
  ([UC-SS-013](UC-SS-013-manage-events.md)); nothing is copied.
- **4a.** A member is **on the source event but no longer on the shared roster** (removed/archived,
  [ADR-0052](../../adr/0052-member-archival-and-permanent-draw-history.md)) → their wishlist is **not**
  carried into the new event (the new event is for the current roster). The carried draw-baseline still
  records the historical pairing for the no-repeat comparison, which resolves archived names by id.
- **4b.** A member is **on the current roster but was not on the source event** → they get an **empty**
  wishlist in the new event (nothing to copy); the organizer fills it in.
- **5a.** No valid assignment exists once the carried baseline is honoured (a small/constrained roster
  where avoiding last year is impossible) → the draw **fails gracefully** and says so; the organizer
  can re-draw ignoring the baseline (an explicit choice), matching FR-004's graceful-failure rule.

## Exceptions

- **Local storage unavailable** → the system reports the error and does not claim the event was
  created.

## Non-functional notes

- **Offline/local-first:** the whole flow works with **no account** — carry-forward is a local
  operation on the one `Group` document; nothing new is sent to the server and the group stays one
  synced unit when signed in
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [ADR-0033](../../adr/0033-which-aggregates-sync.md)). This distinguishes it from the linked-member,
  account-only self-copy on the My-wishlists page ([FR-030](../../requirements/functional/FR-030-edit-my-wishlist.md)).
- **Localization:** the carry-forward offer, its default and the "start blank" choice are in DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** no assignment leaves the device; the carried draw-baseline is the same locally-held
  draw history that already exists per event
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Open questions

- Should the source event's **location/description** (not the date) also be offered for carry-forward?
  Parked as a small extension; v1 carries wishlists + budget + draw-baseline only.
- Should "avoid last year" be a visible toggle at draw time, or only the implicit baseline with the
  graceful-failure escape hatch (5a)? Leaning implicit; revisit after use.

## Traceability

- Requirement: [FR-035](../../requirements/functional/FR-035-carry-forward-new-occasion.md)
- Builds on: [UC-SS-013](UC-SS-013-manage-events.md) (managing events),
  [UC-SS-004](UC-SS-004-run-draw.md) (draw rules & history)
- Governing ADR: [ADR-0042](../../adr/0042-events-as-first-class-entities.md) (events are independent;
  carry-forward is a **copy at creation**, not a live link) — no new ADR needed
- Feature spec: [FEAT-010](../../features/FEAT-010-carry-forward-new-occasion.md)
- Glossary: [Carry-forward](../../glossary/carry-forward.md), [Event](../../glossary/event.md),
  [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md),
  [Draw](../../glossary/draw.md)
