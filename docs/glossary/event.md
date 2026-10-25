---
term: Event
contexts: [Secret-Santa]
status: Draft
---

# Event

## Definition

One **gift-exchange occasion** within a [Group](group.md) (ADR-0042). A group's roster
([Members](member.md)) and [Exclusions](exclusion.md) are **shared**, but each event owns its own
[Draw](draw.md), its own per-member [Wishlists](wishlist.md), its own [Budget](budget.md), and its own
[Event details](event-details.md) (date, location, description). Re-drawing one event never disturbs
another — a birthday exchange and a Christmas exchange for the same people are two events of one group.

A group always has **at least one** event. A group's first/default event is unnamed by default (the UI
shows a generic label for it); a user names an event when they create a second occasion.

## Bounded context(s)

- **Secret-Santa:** an entity of the `Group` aggregate, never a stand-alone aggregate — it lives and
  syncs inside its group (ADR-0042, ADR-0033). The organizer manages events; a [participant](participant.md) sees and
  writes their own wishlist and reads their own assignment **per event**.

## Synonyms / do-not-use

- **Synonyms:** occasion, exchange, round.
- **Do NOT use:** "group" (the group is the container of events, not an event); "draw" (a draw is the
  *result* of running an event, not the event itself); "event details" (that is only the occasion's
  date/location/description, one part of an event).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Anlass |
| EN | Event |
| FR | Événement |

## Related terms

- [Group](group.md), [Draw](draw.md), [Wishlist](wishlist.md), [Budget](budget.md),
  [Event details](event-details.md), [Member](member.md)

## First appearance

- [UC-SS-013](../use-cases/secret-santa/UC-SS-013-manage-events.md); modelled in
  [ADR-0042](../adr/0042-events-as-first-class-entities.md), realised by
  [FEAT-007](../features/FEAT-007-events-per-group.md).
