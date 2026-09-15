---
id: FR-035
title: Carry forward wishlists, budget and prior draw when creating an event
type: functional
status: Implemented
priority: Should
source: UC-SS-014
verification: test
related: [UC-SS-014, FR-002, FR-004, FR-034, FR-030, NFR-001, NFR-002, ADR-0042, ADR-0033, ADR-0052]
---

# FR-035 — Carry forward wishlists, budget and prior draw when creating an event

## Description

When an organizer creates a new [Event](../../glossary/event.md) in a
[Group](../../glossary/group.md) that already has one, they may **carry forward** from a chosen
source event ([Carry-forward](../../glossary/carry-forward.md)). Carry-forward **copies at creation**
(it does not link the two events — they stay independent per
[ADR-0042](../../adr/0042-events-as-first-class-entities.md)):

- **Per-member wishlists** — each **current** roster member's
  [Wishlist](../../glossary/wishlist.md) is copied from the source event. A member on the source event
  who is no longer on the shared roster is **not** copied; a current member absent from the source
  event gets an **empty** wishlist. Copied wishlists are **editable** afterwards.
- **Budget** — the source event's [Budget](../../glossary/budget.md) is copied (editable). The
  event **date is not** carried (each occasion has its own date); carrying location/description is a
  parked extension.
- **Draw-history baseline** — the source event's [Draw](../../glossary/draw.md) is recorded as the new
  event's **previous draw**, so the new event's first draw honours draw rule (c) — *no repeat of the
  previous draw* — and does **not** reproduce the source event's pairings. The new event is created
  **undrawn**.
- **Exclusions are not copied** because they are **already shared** at the group level and apply to
  every event ([FR-034](FR-034-multiple-events-per-group.md)).

Carry-forward is **optional**: the organizer may **start blank**, which is exactly today's add-event
behaviour. It is a **local, account-free** operation — the group remains one synced document
([ADR-0033](../../adr/0033-which-aggregates-sync.md)); nothing new crosses to the server. It is
distinct from the linked-member self-copy on the My-wishlists page
([FR-030](FR-030-edit-my-wishlist.md)), which copies **one** member's own wishlist across their
memberships and needs an account.

## Rationale

The single most-repeated wish in the DE/EN/FR app-store review scan
([competitor research, 2026-09-02](../../brainstorming/2026-09-02-secret-santa-competitor-research.md))
was that copying last year's exchange **loses the wish lists** ("children's lists must be re-typed"),
and the loudest correctness complaint was drawing the **same recipient year after year**. Both are the
*same occasion repeated*, which our model already expresses as a new **event** of one group
([FR-034](FR-034-multiple-events-per-group.md)) — so the fix is to seed that new event from the last
one: wishlists and budget so nothing is re-typed, and the prior draw as the no-repeat baseline so the
draw visibly avoids last year. Because events stay independent copies, the organizer can still edit
everything and the source occasion is never disturbed.

## Acceptance criteria

```gherkin
Given a group whose only event has member wishlists and a budget
When the organizer adds a second event and carries forward from the first
Then each current member's wishlist and the budget are copied into the new event
And the new event is created undrawn
And the first event is unchanged

Given the organizer carries forward from a drawn source event
When they run the draw on the new event
Then the draw does not reproduce the source event's pairings (draw rule c)

Given a member was on the source event but has since been removed from the roster
When the organizer carries forward
Then that member's wishlist is not copied into the new event

Given a member is on the current roster but was not on the source event
When the organizer carries forward
Then that member gets an empty wishlist in the new event

Given the organizer chooses "start blank" instead of carrying forward
When the new event is created
Then nothing is copied and it behaves exactly as adding an event does today

Given the organizer is signed out (account-free)
When they carry forward
Then the operation succeeds entirely on-device and sends nothing to the server

Given a constrained roster where avoiding the carried baseline makes the draw impossible
When the organizer runs the draw
Then it fails gracefully and explains why, and the organizer may re-draw ignoring the baseline
```

## Dependencies & related

- Derived from: [UC-SS-014](../../use-cases/secret-santa/UC-SS-014-carry-forward-new-occasion.md)
- Related: [FR-034](FR-034-multiple-events-per-group.md) (events share roster + exclusions),
  [FR-002](FR-002-manage-members-and-wishlists.md) (wishlists),
  [FR-004](FR-004-run-fair-draw.md) (draw rules & history — rule (c) is what the baseline feeds),
  [FR-030](FR-030-edit-my-wishlist.md) (the distinct linked-member self-copy)
- Governing ADR: [ADR-0042](../../adr/0042-events-as-first-class-entities.md) (events are independent →
  carry-forward is a copy, not a link); [ADR-0033](../../adr/0033-which-aggregates-sync.md) (group stays
  one sync unit — no sync change); [ADR-0052](../../adr/0052-member-archival-and-permanent-draw-history.md)
  (archived members & permanent draw history — how the baseline resolves removed members). **No new ADR
  required.**
- Implemented by: [FEAT-010](../../features/FEAT-010-carry-forward-new-occasion.md), verified by
  [TP-010](../../features/TP-010-carry-forward-new-occasion.md)
- Glossary: [Carry-forward](../../glossary/carry-forward.md), [Event](../../glossary/event.md),
  [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md),
  [Draw](../../glossary/draw.md)
