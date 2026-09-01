---
status: Accepted
date: 2026-08-14
deciders: [Adrian]
tags: [architecture, domain, secret-santa, ddd, sync]
---

# ADR-0042 — Events as first-class entities within a Secret-Santa group

## Context and problem statement

Today a [Group](../glossary/group.md) models **one** gift exchange: a single optional
[Event details](../glossary/event-details.md) value, a single [Budget](../glossary/budget.md), one
per-[Member](../glossary/member.md) [Wishlist](../glossary/wishlist.md), and a single "current"
[Draw](../glossary/draw.md) that re-drawing **replaces**. Organizers asked to reuse the *same people*
across **several occasions** — Christmas, then a birthday — each with its **own draw** and its **own
wishlists**, without retyping the roster each time.

So the question: **how do we model repeated occasions inside a group, and what is shared across them
versus owned per occasion — in the domain, in the local store and its migration, and in the
server-side linking projection that a signed-in member reads?**

## Decision drivers

- **Reuse the roster; keep occasions independent.** The whole point is to type the people once. But a
  draw belongs to *one occasion* — re-drawing Christmas must not disturb the birthday exchange — and so
  do the wishlists people write for that occasion, the budget, and the date.
- **A draw belongs to an exact member set.** The existing invariant (adding/removing a member voids the
  draw) must hold per occasion: changing the shared roster voids **every** occasion's draw.
- **Migrate in place, lose nothing.** Every existing group — its draw history, budget, event details,
  and each member's wishlist — must survive the reshape. The local store holds the only copy
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Stay one sync aggregate.** ADR-0033 syncs the whole `Group` aggregate as one opaque document. The
  reshape must keep events *inside* that aggregate so the sync catalogue, LWW and claim flow are
  unchanged.
- **The signed-in projection must follow.** Per-event wishlists and assignments mean a linked member's
  server-side My-wishlist and My-assignment are now **per event**; the linking store
  ([ADR-0020](0020-server-side-assignment-privacy.md)) must key them by event, not just by group.

## Considered options

1. **`Event` as a first-class entity inside the `Group` aggregate.** Members and exclusions stay on the
   group (shared); each `Event` owns its own draw history, per-member wishlists, budget and event
   details. The linking store gains an event dimension.
2. **One `Group` per occasion.** Copy the roster into a new group each time. Rejected: it defeats the
   entire request (retyping people) and scatters "the same exchange" across unrelated aggregates.
3. **Multiple draws, but a single shared wishlist/budget/date.** A group keeps one wishlist per member
   and one budget, and only the *draw* is repeated. Rejected: the organizer explicitly wants a separate
   wishlist and budget per occasion (a birthday list is not a Christmas list).

## Decision outcome

**Chosen: Option 1.** A **Secret-Santa `Group` owns a shared roster (`Members`) and shared
`Exclusions`, plus one or more `Event`s.** An `Event` is an entity of the `Group` aggregate and owns:

- its **name**,
- its optional **budget** and **event details** (date, location, description),
- its **draw history** (with the current draw and the "invalidated by membership change" flag), and
- its **per-member wishlists** — a wishlist per member, scoped to that event.

**`Wishlist` moves off `Member`.** A member is now identity within the group (name, optional account
link, circle origin); what they *want* is per-event. Adding a member voids **every** event's draw;
removing a member voids every event's draw **and** drops that member's wishlist in every event.

**A group always has at least one event.** Creating a group creates a default event; the migration
below guarantees existing groups get one too. `Budget`, `EventDetails`, `Draw`, `Assignment` and the
`DrawGenerator` domain service are **reused unchanged** — only their *ownership* moves from `Group`
(or `Member`) down to `Event`.

### Local persistence & migration (in place, no loss)

`GroupState` gains `IReadOnlyList<EventState> Events`, where an `EventState` carries the event's name,
budget, event details, draw history, and its per-member wishlists. The **legacy** `GroupState` fields
(`Budget`, `Event`, `Draws`) and `MemberState.Wishlist` are **kept readable**. `Group.FromState`
synthesizes a single default `Event` from the legacy fields when `Events` is empty — inheriting the old
budget, event details, draw history/invalidation flag, and each member's legacy wishlist — exactly the
additive back-compat convention `MemberState.UserId` and `DrawInvalidatedByMembershipChange` already
use. New groups write the `Events` shape; a re-save drops the legacy fields naturally.

### Sync

Unchanged catalogue: the **whole `Group` aggregate (now including its events)** remains the single
opaque sync document (ADR-0033/ADR-0016). Events add no new sync kind and no new server sync table.

### Server-side linking projection (per event)

The M4 linking store (ADR-0020) becomes **event-scoped**: `AssignmentRow` and `MemberWishlistRow` gain
an `EventId`, so a linked member has one published assignment and one self-owned wishlist **per event**.
The publish, my-assignment, my-wishlist and my-memberships contracts and endpoints carry the event id,
and `GetMyAssignments`/`GetMyWishlists` return event-tagged rows. This is an additive EF migration on
the `linking` schema. Member **links** themselves stay group-scoped — being *in* the group is not
per-event, only what you wish for and are assigned is.

### Reveal links

Unchanged mechanism: a [reveal link](../glossary/reveal-link.md) carries its assignment payload
self-contained in the URL fragment (ADR-0014), independent of group/event structure, so **already-issued
links keep working** and new ones are simply generated per event.

### UI: two-level navigation

Group-level sections (details, **members**, **exclusions**, **events list**) stay at
`/groups/{id}/…`; event-level sections (**wishlists**, **draw**, **share**) move under
`/groups/{id}/events/{eventId}/…`. This amends the ADR-0040 group sub-navigation.

Options 2 and 3 are rejected for the reasons under the drivers.

## Consequences

- **Positive:** organizers reuse one roster across many occasions, each fully independent (own draw,
  wishlists, budget, date). The core draw invariant is preserved per event.
- **Positive:** sync is untouched — one aggregate, one document, one catalogue entry.
- **Negative / accepted (scope):** this is a broad change. It reshapes the domain, the local memento
  (with a migration), the contracts, every draw/wishlist/budget handler, the two-level UI, **and** the
  Main linking store (an EF migration keying assignments and wishlists by event). Accepted deliberately
  — the organizer chose the full per-event experience including the signed-in projection.
- **Negative / accepted (UX shift):** wishlists move from the group-level members screen to a per-event
  screen; the members screen becomes roster-only (names, links). This is the correct home for a
  per-event wishlist and is called out in the feature spec.
- **Follow-ups:** [FEAT-007](../features/FEAT-007-events-per-group.md) and its test plan; a new
  [Event](../glossary/event.md) glossary term; `event-details.md`, `draw.md` and `wishlist.md` updated
  to note event ownership; the ADR-0040 nav amendment recorded in FEAT-007.
- **New risks:** the linking-store migration must backfill existing per-group assignment/wishlist rows
  onto the migrated group's **default event**, mirroring the local default-event migration, or a linked
  member's already-published assignment would orphan. The migration owns this backfill.

## Pros and cons of the options

### 1. `Event` entity within the `Group` aggregate (chosen)
- 👍 Reuses the roster; each occasion independent; one sync aggregate; draw invariant preserved per event.
- 👍 Migrates in place with the established additive convention.
- 👎 Broad: domain + memento + contracts + handlers + two-level UI + linking-store EF migration.

### 2. One group per occasion
- 👍 No aggregate reshape.
- 👎 Defeats the request — retype the roster every time; "the same exchange" is scattered across groups.

### 3. Multiple draws, shared wishlist/budget/date
- 👍 Smaller change (draw history already exists).
- 👎 A birthday list is not a Christmas list — the organizer asked for per-occasion wishlists and budget.

## Amendments

### 2026-09-01 — a new group starts with **no** event

The original decision seeded every new group with **one** default event (its id equal to the group
id) and forbade removing a group's last event ("at least one event at all times"). In use this
pre-created and pre-named an *unnamed* occasion the user never asked for, and the group editor opened
onto an event rather than letting the user create and name their first occasion.

Changed so that **`Group.Create` adds no event**; the user creates the first occasion themselves, and
the UI pre-fills its name with the localized **"Christmas {year}"** default (DE *Weihnachten*, FR
*Noël*). `Group.RemoveEvent` no longer refuses to remove the last event, so a group may hold **zero**
occasions. The create-from-circle flow is eventless too.

Persistence and migration are unaffected in spirit and made precise: a **pre-events** snapshot still
has a `null` `Events` field and is migrated to a single default event; an **empty** (non-null)
`Events` list is now a real, intentional state and is kept as-is. The default-event-id-equals-group-id
convention remains only for that legacy migration. One sync aggregate, LWW and claim flow are
unchanged (an eventless group is a smaller document, nothing more).

## Links

- Related ADRs: [ADR-0040](0040-ui-component-library-mudblazor.md) (**nav amended** — two-level group
  navigation), [ADR-0020](0020-server-side-assignment-privacy.md) (**linking store extended** — per
  event), [ADR-0033](0033-which-aggregates-sync.md) (sync catalogue unchanged),
  [ADR-0014](0014-reveal-link-encoding-and-privacy.md) (reveal links unchanged),
  [ADR-0019](0019-member-user-linking.md), [ADR-0016](0016-sync-and-conflict-resolution.md)
- Requirements / use cases: [FR-034](../requirements/functional/FR-034-multiple-events-per-group.md),
  [UC-SS-013](../use-cases/secret-santa/UC-SS-013-manage-events.md), [FR-001](../requirements/functional/FR-001-create-secret-santa-group.md),
  [FR-002](../requirements/functional/FR-002-manage-members-and-wishlists.md),
  [FR-004](../requirements/functional/FR-004-run-fair-draw.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Feature: [FEAT-007](../features/FEAT-007-events-per-group.md)
- Glossary: [Event](../glossary/event.md), [Group](../glossary/group.md), [Draw](../glossary/draw.md),
  [Wishlist](../glossary/wishlist.md), [Event details](../glossary/event-details.md)
