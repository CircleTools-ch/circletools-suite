---
id: FR-042
title: An admin can release a claimed person so they can be claimed again
type: functional
status: Approved
priority: Should
source: UC-SS-020
verification: test
related: [UC-SS-020, FR-041, NFR-002, ADR-0058]
---

# FR-042 — An admin can release a claimed person so they can be claimed again

## Description

In a [shared-by-link group](../../glossary/shared-by-link-group.md), a
[person claim](../../glossary/person-claim.md) **locks** the [Member](../../glossary/member.md) it binds
([FR-041](FR-041-join-link-and-person-claim.md)). An **admin can release** a claimed member, which
unbinds it (from the account or device that held it) and makes it **claimable again** through the
[join link](../../glossary/join-link.md) ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)).

Release is the **single recovery mechanism** for every "wrong person / lost access / changed device"
situation:

- a participant **replaces or loses the device** that held a device-only claim;
- someone **claimed the wrong member** (or an account-linked person needs re-assigning);
- an admin needs to **re-open a spot**.

Release:

- is an **admin-only** action, available from the members screen of a shared-by-link group;
- **does not delete the member** — the roster, wishlist, gift note and any drawn assignment stay; only
  the *claim binding* is removed;
- takes effect immediately, after which the member reads as **not claimed** and the join link may claim
  it afresh.

There is no self-service release — a device that has lost access cannot free itself, by design (that is
what makes the lock meaningful), so recovery always runs through an admin.

## Rationale

First-claim-locks ([FR-041](FR-041-join-link-and-person-claim.md)) is what keeps the draw non-spoiling
under a single shared secret, but a permanent lock with no escape would strand anyone who changes phone
or mis-taps. Making **one** admin-controlled release the sole recovery path keeps the model simple (a
single mechanism covers lost device, new device, and mis-claim) and keeps control with the organizer, so
a locked spot can only be reopened by someone trusted with the group.

## Acceptance criteria

```gherkin
Given a member claimed on a device that the participant no longer has
When an admin releases that member
Then the member reads as not claimed and can be claimed again from the join link
And the member's wishlist, gift note and any drawn assignment are unchanged

Given a member claimed to an account
When an admin releases that member
Then the account no longer resolves to that member and the spot is open again

Given a non-admin holding only the join link
When they attempt to release a claimed member
Then the action is not available to them

Given an admin releases a member and someone re-claims it
Then only the new claimant sees that member's assignment; the previous holder no longer does
```

## Dependencies & related

- Derived from: [UC-SS-020](../../use-cases/secret-santa/UC-SS-020-release-a-claimed-person.md)
- Related: [FR-041](FR-041-join-link-and-person-claim.md) (the claim + lock this reverses),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (re-claim moves who can read the assignment)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Person claim](../../glossary/person-claim.md), [Member](../../glossary/member.md),
  [Join link](../../glossary/join-link.md), [Shared-by-link group](../../glossary/shared-by-link-group.md)
