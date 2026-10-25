---
term: Participant contribution
contexts: [Secret-Santa]
status: Superseded
---

# Participant contribution

> **Replaced by [Member entry](member-entry.md)** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)): one copy in the group store, merged into the document on every write. The entry below is kept for history.

## Definition

The part of a [shared-by-link group](shared-by-link-group.md) that a joined participant maintains
**themselves**: their own [gift note](gift-note.md) and their own [wishlist](wishlist.md) per
[event](event.md) ([ADR-0080](../adr/0080-participant-access-to-shared-groups.md)). The server records it
per claimed [member](member.md) and keeps it when the organizer uploads an older copy of the group. While
the member is claimed, the organizer sees these fields but cannot change them; releasing the claim (or
the participant leaving) removes the contribution.

## Bounded context(s)

- **Secret-Santa:** participant-owned fields of a claimed member.

## Synonyms / do-not-use

- **Synonyms:** "my entry" (product phrasing).
- **Do NOT use:** "self-owned wishlist" for this (that is the account-linked member's wishlist, FR-030).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Deine Angaben |
| EN | Your details |
| FR | Vos informations |

## Related terms

- [Person claim](person-claim.md), [Participant key](participant-key.md), [Wishlist](wishlist.md),
  [Gift note](gift-note.md)

## First appearance

- [ADR-0080](../adr/0080-participant-access-to-shared-groups.md), [FEAT-024](../features/FEAT-024-participants-in-shared-groups.md).
