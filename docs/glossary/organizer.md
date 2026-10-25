---
term: Organizer
contexts: [Secret-Santa]
status: Agreed
---

# Organizer

## Definition

The person who manages a [Group](group.md): edits the roster, the events and the exclusions, runs the draw and
hands out links. For a group on this device it is whoever uses that device; for an [online group](online-group.md)
it is whoever holds the [owner](owner.md) role (later also an admin role) — through their account or through the
[organizer link](organizer-link.md).

An organizer may also be in the roster; they then see their own assignment like any participant ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)).

## Bounded context(s)

- **Secret-Santa:** a person's relation to a group. In code, the permissions decide (`ReadDocument`, `WriteDocument`, …), not a flag named "organizer".

## Synonyms / do-not-use

- **Synonyms:** organiser (British spelling in prose is fine; code uses "organizer").
- **Do NOT use:** "admin" for today's single organizing role (admin is the planned second organizing role); "creator".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Organisator:in (organisierende Person) |
| EN | Organizer |
| FR | Organisateur·rice |

## Related terms

- [Owner](owner.md), [Role assignment](role-assignment.md), [Organizer link](organizer-link.md), [Participant](participant.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
