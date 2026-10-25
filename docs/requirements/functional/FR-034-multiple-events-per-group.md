---
id: FR-034
title: Multiple events (occasions) per group
type: functional
status: Implemented
priority: Should
source: UC-SS-013
verification: test
related: [UC-SS-013, FR-001, FR-002, FR-004, FR-005, FR-029, FR-030, NFR-001, NFR-002, ADR-0042, ADR-0033, ADR-0020]
---

# FR-034 — Multiple events (occasions) per group

## Description

A [Group](../../glossary/group.md) reuses **one shared roster** ([Members](../../glossary/member.md))
and **shared** [Exclusions](../../glossary/exclusion.md) across **one or more**
[Events](../../glossary/event.md) — occasions such as "Christmas 2026" and "Birthday 2027" — without
re-entering the people each time (ADR-0042).

- Each event owns its own [Draw](../../glossary/draw.md) (with its own history and re-draw), its own
  per-member [Wishlist](../../glossary/wishlist.md), its own [Budget](../../glossary/budget.md), and
  its own [Event details](../../glossary/event-details.md) (date, location, description). Running or
  re-running one event's draw **does not** affect any other event.
- A group **always has at least one event.** Creating a group creates a default event; the last
  remaining event cannot be removed.
- Adding or removing a member (a change to the **shared** roster) **voids the draw of every event** —
  a draw belongs to the exact roster it was generated for. Renaming a member, or someone taking part as one,
  does not.
- Wishlists are **per event**: a member's Christmas list is not their birthday list. The organizer
  sets any member's wishlist per event; a [participant](../../glossary/participant.md) owns and edits **their own** wishlist per
  event ([FR-030](FR-030-edit-my-wishlist.md)), and reads **their own** assignment per event
  ([FR-029](FR-029-see-my-assignment.md)).
- [Reveal links](../../glossary/reveal-link.md) are generated **per event** and carry that event's
  budget, date and the recipient's wishlist for that event. Existing (already-issued) links keep
  working — a reveal link is self-contained and independent of group/event structure
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)).

## Rationale

The same group of people exchange gifts on more than one occasion, and each occasion is its own
event: a fresh draw, fresh wishlists, its own budget and date. Modelling those as repeated **events**
of one group — rather than a new group each time — means the roster and exclusions are typed **once**
and reused, which is exactly what the organizer asked for. Keeping members and exclusions shared while
everything occasion-specific lives on the event is what makes "reuse the people, vary the occasion"
both possible and safe (the draw invariant still holds, per event).

Events stay **inside** the `Group` aggregate, so nothing about sync changes: a group is still one
synced document ([ADR-0033](../../adr/0033-which-aggregates-sync.md)), and the local-first, offline
promise is unweakened ([NFR-001](../non-functional/NFR-001-local-first-offline.md)).

## Acceptance criteria

```gherkin
Given a group with a shared roster
When the organizer creates a second event
Then the new event reuses the same members and exclusions
And it has its own draw, wishlists, budget and date

Given a group with two drawn events
When the organizer re-draws one event
Then the other event's draw is unchanged

Given a group with two events
When a member is added to or removed from the shared roster
Then the draw of every event is voided

Given a participant in a group with two events
When they view their wishlists
Then they can set a separate wishlist for each event

Given a participant in a drawn event
When they read their assignment
Then they see one assignment per drawn event they are in

Given a group with a single event
When the organizer tries to remove that event
Then it is refused, because a group must keep at least one event

Given a pre-existing (single-exchange) group created before events
When it is opened after the change
Then its draw, budget, event details and wishlists are intact inside one default event
```

## Amendment (2026-10-16) — event cards and the add dialog

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q22, I5):

- [ ] Each event card shows its date and countdown ("24.12.2026 · in 69 days" or "No date yet") and the
      wishlist progress ("Wishlists 2/4"); events are ordered upcoming first, then undated, past ones last and
      muted.
- [ ] The event hub's *Details* tile summarises what is set ("24.12.2026 · up to CHF 30 · At Grandma's") or
      says what is missing ("Date and budget missing").
- [ ] *Add event* offers an optional date field, for the first event as for later ones.

## Dependencies & related

- Derived from: [UC-SS-013](../../use-cases/secret-santa/UC-SS-013-manage-events.md)
- Related: [FR-001](FR-001-create-secret-santa-group.md) (create a group),
  [FR-002](FR-002-manage-members-and-wishlists.md) (the roster; wishlists are now per event),
  [FR-004](FR-004-run-fair-draw.md) (the draw, now per event),
  [FR-005](FR-005-generate-reveal-links.md) (reveal links, now per event),
  [FR-029](FR-029-see-my-assignment.md), [FR-030](FR-030-edit-my-wishlist.md)
- Governing ADRs: [ADR-0042](../../adr/0042-events-as-first-class-entities.md) (the model and its
  migration), [ADR-0033](../../adr/0033-which-aggregates-sync.md) (the group stays one sync unit),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (the linking projection, now per event)
- Implemented by: [FEAT-007](../../features/FEAT-007-events-per-group.md), verified by
  [TP-007](../../features/TP-007-events-per-group.md)
- Glossary: [Event](../../glossary/event.md), [Group](../../glossary/group.md),
  [Draw](../../glossary/draw.md), [Wishlist](../../glossary/wishlist.md)
