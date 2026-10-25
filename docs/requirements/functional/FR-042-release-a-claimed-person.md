---
id: FR-042
title: The organizer can free a participant's spot so the member can be taken again
type: functional
status: Implemented
priority: Should
source: UC-SS-020
verification: test
related: [UC-SS-020, FR-041, NFR-002, ADR-0086, FEAT-027]
---

# FR-042 — The organizer can free a participant's spot so the member can be taken again

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

From a member's row on the members page, an organizer can **free the spot** of a member who has a
[participant](../../glossary/participant.md) — or withdraw an invitation still waiting for an answer. After confirming:

- the participant's role goes: their device or account no longer reads the group;
- what they wrote — their [member entry](../../glossary/member-entry.md) — stays in the group, and the organizer can edit it
  from then on;
- the member is free again, so the person can take part anew (for example from a new phone) through the join link.

A participant can also **leave** a group themselves, with the same effect.

## Rationale

A device gets lost and a person picks the wrong name; without a way back, the member would be stuck. Freeing the
spot is the organizer's recovery tool, and keeping what was written means nobody loses their wishlist.

## Acceptance criteria

```gherkin
Given a member with a participant
When the organizer frees the spot and confirms
Then the participant can no longer read the group, and the member shows no participant

Given the freed member had written a wishlist
When the organizer opens the event's wishlists
Then the wishlist is still there and the organizer can edit it

Given the spot was freed
When a person opens the join link
Then the member can be picked again
```

## Dependencies & related

- Derived from: [UC-SS-020](../../use-cases/secret-santa/UC-SS-020-release-a-claimed-person.md)
- Related: [FR-041](FR-041-join-link-and-person-claim.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
