---
term: Online group
contexts: [Secret-Santa]
status: Agreed
---

# Online group

## Definition

A [Group](group.md) kept **online**: one copy on the server, in the Secret-Santa group store ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). Who may do
what in it is a list of [role assignments](role-assignment.md): exactly one [owner](owner.md) — an account or an
[organizer link](organizer-link.md) — and any number of [participants](participant.md).

The server reads and merges the group's document, so every view is computed from that one copy when it is read:
the organizer's full document, a participant's own pairing, the page behind a [reveal link](reveal-link.md).
A group no account owns is deleted after a year without use
([NFR-010](../requirements/non-functional/NFR-010-shared-group-retention-lifecycle.md)).

## Bounded context(s)

- **Secret-Santa:** a group whose [place](place.md) is *online*. The same domain group as one on a device; only where it is kept, and who reaches it, differ.

## Synonyms / do-not-use

- **Synonyms:** group kept online.
- **Do NOT use:** "shared-by-link group", "account group", "cloud group" — there is one kind of online group, whatever owns it.

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Online-Gruppe |
| EN | Online group |
| FR | Groupe en ligne |

## Related terms

- [Place](place.md), [Role assignment](role-assignment.md), [Owner](owner.md), [Participant](participant.md), [Organizer link](organizer-link.md), [Join link](join-link.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
