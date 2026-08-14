---
id: FEAT-007
title: Events (occasions) as first-class entities within a group
context: SecretSanta
status: Done
related: [FR-034, UC-SS-013, ADR-0042, ADR-0033, ADR-0040, ADR-0020, TP-007]
---

# FEAT-007 — Events (occasions) as first-class entities within a group

## Summary

Turn a Secret-Santa [Group](../glossary/group.md) from a single gift exchange into a **shared roster
that hosts one or more [Events](../glossary/event.md)** (occasions), each with its own
[Draw](../glossary/draw.md), per-member [Wishlists](../glossary/wishlist.md),
[Budget](../glossary/budget.md) and [Event details](../glossary/event-details.md). Realises
[FR-034](../requirements/functional/FR-034-multiple-events-per-group.md) /
[UC-SS-013](../use-cases/secret-santa/UC-SS-013-manage-events.md) under
[ADR-0042](../adr/0042-events-as-first-class-entities.md).

## Design

### Domain (`SecretSanta.Core.Domain`)
- New `Event` entity owned by `Group`: name, optional budget and event details, its own draw history
  (`CurrentDraw`, `DrawInvalidatedByMembershipChange`), and per-member wishlists.
- `Group` keeps **shared** `Members` and `Exclusions` and gains `Events`; draw/budget/event-details and
  wishlists are **event-scoped**. `Member` no longer holds a wishlist. Adding/removing a member voids
  **every** event's draw and adds/removes that member's wishlist slot across events. A group always has
  ≥ 1 event; the first/default event's id **equals the group id** (a deterministic value the server
  migration reuses).

### Persistence & migration (`Core/Persistence`, `LocalStore`)
- `GroupState` gains `Events` (`EventState` with its own budget, event details, draws, invalidation
  flag and per-member `EventWishlistState`). The legacy single-exchange fields (`Budget`, `Event`,
  `Draws`, `MemberState.Wishlist`) are kept **readable**; `Group.FromState` migrates a pre-events
  snapshot into **one default event** (id = group id) carrying the legacy budget, event details, draw
  history and each member's legacy wishlist. Additive, in place, no data loss.

### Contracts & application (`Contracts`, `Core/Application`)
- New `EventSummaryDto` / `EventDto` / `MemberWishlistDto`; `GroupDto` is roster + exclusions + event
  summaries; `MemberDto` loses its wishlist. New handlers `AddEvent` / `RenameEvent` / `RemoveEvent` /
  `GetEvent` / `SetMemberWishlist`; `RunDraw` / `SetBudget` / `SetEventDetails` become event-scoped.

### Sync
- Unchanged catalogue: the whole `Group` (with its events) is still one opaque synced document
  ([ADR-0033](../adr/0033-which-aggregates-sync.md)). No new sync kind, no group-store migration.

### Server linking projection (`Main.Service`, per event — [ADR-0020](../adr/0020-server-side-assignment-privacy.md))
- `AssignmentRow` and `MemberWishlistRow` are keyed by **(group, event, account)**; a new
  `GroupEventRow` **registry** lets a linked member on another device enumerate a group's events to
  write a per-event wishlist before a draw. Publish/my-assignment/my-wishlist/my-memberships carry the
  event; the organizer's client publishes the registry each sync. Additive EF migration on the
  `linking` schema backfills existing rows onto the default event (id = group id) and seeds the
  registry from existing member-links.

### UI ([ADR-0040](../adr/0040-ui-component-library-mudblazor.md) amendment — two-level navigation)
- Group level (`/groups/{id}`): the events list + rename, plus **Members** and **Exclusions**.
- Event level (`/groups/{id}/events/{eventId}/…`): **Details** (name, budget, date), **Wishlists**,
  **Draw**, **Share**. New `EventNav`; the members screen is roster-only; My-wishlists / My-assignments
  are per (group, event).

## Traceability

- Requirement: [FR-034](../requirements/functional/FR-034-multiple-events-per-group.md)
- Use case: [UC-SS-013](../use-cases/secret-santa/UC-SS-013-manage-events.md)
- ADRs: [ADR-0042](../adr/0042-events-as-first-class-entities.md) (model + migration),
  [ADR-0033](../adr/0033-which-aggregates-sync.md) (sync unchanged),
  [ADR-0040](../adr/0040-ui-component-library-mudblazor.md) (nav amended),
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md) (linking per event)
- Test plan: [TP-007](TP-007-events-per-group.md)
- Glossary: [Event](../glossary/event.md), [Group](../glossary/group.md), [Draw](../glossary/draw.md),
  [Wishlist](../glossary/wishlist.md)
