---
id: FR-041
title: A join link lets a person join a shared group and claim which member they are
type: functional
status: Draft
priority: Must
source: UC-SS-019
verification: test
related: [UC-SS-019, FR-039, FR-040, FR-042, NFR-002, ADR-0058, ADR-0017, ADR-0019, ADR-0043]
---

# FR-041 — A join link lets a person join a shared group and claim which member they are

## Description

A [shared-by-link group](../../glossary/shared-by-link-group.md) exposes a
[join link](../../glossary/join-link.md) that lets a person **join as a participant** — with **or
without an account** ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)). Opening it runs a
short *"who are you in this group?"* flow with **two independent choices**:

- **Which person (a [person claim](../../glossary/person-claim.md)):** claim an existing
  [Member](../../glossary/member.md) in the roster ("I'm Grandma"), **or** add a new person (yourself)
  to the group.
- **How remembered:** link the chosen member to an **account** (durable, cross-device — reusing
  member↔user linking, [ADR-0019](../../adr/0019-member-user-linking.md), and invitee-chosen promotion,
  [ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)), **or** to **just this device**
  (a device-local claim; nothing synced beyond the device).

The four resulting combinations — existing/new person × account/device-only — are all valid. A joined
participant then sees the group and edits **their own** [Wishlist](../../glossary/wishlist.md) /
[Gift note](../../glossary/gift-note.md), and sees **their own** [Assignment](../../glossary/assignment.md)
once drawn — never another member's ([NFR-002](../non-functional/NFR-002-assignment-privacy.md)).

A person claim **first-claim-locks**:

- Once a member is claimed (by an account **or** a device), that member **cannot be claimed again** — a
  device claim is **not** silently overridden by a later account claim.
- The only way to make a locked member claimable again is an **admin release**
  ([FR-042](FR-042-release-a-claimed-person.md)).

The join link is independent of the [admin link](../../glossary/admin-link.md): it **keeps working**
after an account [claims admin](../../glossary/claim.md) — only the admin capability locks, participants
are unaffected.

## Rationale

The middle storage tier exists so co-organizers and participants can act on a group **without signing
up** ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md), from the
[storage & sharing tiers session](../../brainstorming/2026-09-13-storage-and-sharing-tiers.md)). Letting
a joiner say *which person they are* is what turns "a link to a group" into "my place in the group" —
and reusing the existing member↔user linking means an account joiner needs no new plumbing. First-claim
-locks is the minimum that keeps the non-spoiling draw intact under a shared secret: it stops a second
person quietly taking a member who is already in use, while the family/friend trust context and the
admin-release escape absorb the rest (secrecy here is trust-bounded, not cryptographic — see NFR-002).

## Acceptance criteria

```gherkin
Given a shared-by-link group with an unclaimed member "Grandma"
When a person opens the join link, claims "Grandma", and chooses "just this device"
Then they are joined as Grandma on that device with no account
And after the draw they see only Grandma's assignment, never another member's

Given the same join link
When a person chooses "add me as a new person" and signs in to an account
Then a new member is added and linked to that account, visible on their other signed-in devices

Given member "Grandma" is already claimed
When another join-link holder tries to claim "Grandma"
Then the claim is refused and the member is shown as already claimed

Given a member was claimed on a device
When someone later tries to claim the same member from an account
Then the claim is refused (no silent override) until an admin releases the member

Given a participant joined via the join link
When an account later claims admin for the group
Then the join link still works and the participant keeps their access and assignment
```

## Dependencies & related

- Derived from: [UC-SS-019](../../use-cases/secret-santa/UC-SS-019-join-a-shared-group.md)
- Related: [FR-039](FR-039-shared-by-link-storage-and-sync.md) (the group is on the server to join),
  [FR-040](FR-040-admin-link-capability.md) (the admin link, retired on account claim — the join link is
  not), [FR-042](FR-042-release-a-claimed-person.md) (release a locked member),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (a joiner sees only their own assignment)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md); reuses
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (claim shape),
  [ADR-0019](../../adr/0019-member-user-linking.md) and
  [ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md) (member↔user linking)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Join link](../../glossary/join-link.md), [Person claim](../../glossary/person-claim.md),
  [Member](../../glossary/member.md), [Assignment](../../glossary/assignment.md),
  [Shared-by-link group](../../glossary/shared-by-link-group.md)
