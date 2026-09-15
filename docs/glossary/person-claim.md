---
term: Person claim
contexts: [Secret-Santa]
status: Draft
---

# Person claim

## Definition

In a [shared-by-link group](shared-by-link-group.md), the act of a participant asserting **which
[Member](member.md) they are** — done through the [join link](join-link.md) — so they may see that
member's own [Assignment](assignment.md) and edit that member's [wishlist](wishlist.md) /
[gift note](gift-note.md) ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). A claim binds a
member to either an account or a single device.

Rules (the basis of the mode's trust-bounded privacy,
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)):

- **First claim wins, and it locks.** Once a member is claimed, that member **cannot be claimed again**
  — a device claim is not silently overridden by a later account claim.
- **Release** is the inverse and the **only recovery** for a new/lost device or a mis-claim: **only an
  admin** can free a claimed member, after which the join link can claim it afresh.

Distinct from [Claim](claim.md) (taking a *whole group* into a *User account*); a person claim is about
*which member you are* inside a shared group, not about who owns the group.

## Bounded context(s)

- **Secret-Santa:** identifying a joiner with a member of the roster.

## Synonyms / do-not-use

- **Synonyms:** "this is me" (product phrasing); claiming a member/spot.
- **Do NOT use:** plain "claim" unqualified (collides with the group→account [Claim](claim.md)); "login"
  (a device-only person claim involves no account).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Das bin ich |
| EN | This is me |
| FR | C'est moi |

## Related terms

- [Join link](join-link.md), [Member](member.md), [Shared-by-link group](shared-by-link-group.md),
  [Claim](claim.md), [Assignment](assignment.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md).
