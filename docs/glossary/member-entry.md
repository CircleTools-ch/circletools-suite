---
term: Member entry
contexts: [Secret-Santa]
status: Agreed
---

# Member entry

## Definition

What a [Member](member.md) keeps **themselves** in an [online group](online-group.md): their
[gift note](gift-note.md) and their [wishlist](wishlist.md) per event ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). It is written by the member's
[participant](participant.md) — or, for a member without one, from their [reveal link](reveal-link.md) — and
stored once, in the group store.

The store merges it into the group's document on every write and every organizer push, so the organizer's copy,
the participant's view and the giver's reveal page all show the same text, and an organizer's older copy never
overwrites it. Leaving or being released keeps the entry in the group; the organizer can edit it again from then on.

## Bounded context(s)

- **Secret-Santa:** a row of the group store (`member_entries`), with who keeps it (participant or reveal link).

## Synonyms / do-not-use

- **Synonyms:** own entry, own wishlist and gift note.
- **Do NOT use:** "participant contribution", "self-wishlist" (the old model's terms).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Eigene Wunschliste und Hinweis |
| EN | Own wishlist and note |
| FR | Propre liste et remarque |

## Related terms

- [Participant](participant.md), [Wishlist](wishlist.md), [Gift note](gift-note.md), [Reveal link](reveal-link.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
