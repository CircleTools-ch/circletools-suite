---
term: Participant key
contexts: [Secret-Santa]
status: Superseded
---

# Participant key

> **Replaced by [Device key](device-key.md)** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). The entry below is kept for history.

## Definition

A secret the server issues to a device when it makes a [person claim](person-claim.md) in a
[shared-by-link group](shared-by-link-group.md). The device presents it with later requests to prove
**which member it speaks for**, so the server can return that member's own
[assignment](assignment.md) and accept that member's own entry
([ADR-0080](../adr/0080-participant-access-to-shared-groups.md)). The server stores only its hash, as for a
[share token](share-token.md). A claim made with an account is also proven by that account's sign-in, on
any of its devices.

## Bounded context(s)

- **Secret-Santa:** proof of a person claim; never shown to the user.

## Synonyms / do-not-use

- **Synonyms:** —
- **Do NOT use:** "device id" or "device handle" (the unstable identifier it replaces); "token" alone
  (collides with [share token](share-token.md)).

## Product display terms (reference only — not code names)

Not shown in the product.

## Related terms

- [Person claim](person-claim.md), [Participant contribution](participant-contribution.md),
  [Join link](join-link.md), [Share token](share-token.md)

## First appearance

- [ADR-0080](../adr/0080-participant-access-to-shared-groups.md), [FEAT-024](../features/FEAT-024-participants-in-shared-groups.md).
