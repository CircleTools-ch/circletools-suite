---
id: FR-029
title: See my own assignment in the app
type: functional
status: Implemented
priority: Must
source: UC-SS-011
verification: test
related: [UC-SS-011, UC-SS-010, UC-SS-006, FR-041, FR-032, NFR-002, ADR-0086, FEAT-027]
---

# FR-029 — See my own assignment in the app

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

A [participant](../../glossary/participant.md) sees their own assignment on their **participant page** (opened from the group
list):

- per event, once drawn: whom they give to, behind a deliberate *Reveal* step, then that person's wishlist and gift
  note, the budget, the place and an *add to calendar* action;
- before the draw: that the names have not been drawn yet;
- nothing about anyone else's pairing ([NFR-002](../non-functional/NFR-002-assignment-privacy.md)).

An **organizer who is also in the roster** sees the same through *My assignment* on the group's page. The page
shows the last view the device received when offline.

## Rationale

A person taking part should not need their reveal link to find out whom they give to; the app on their own device
is enough. The organizer who takes part is no exception.

## Acceptance criteria

```gherkin
Given a participant and a drawn event
When they open the group and reveal
Then they see whom they give to, with that person's wishlist and gift note, and no other pairing

Given an event not drawn yet
When the participant opens the group
Then the page says the names have not been drawn yet

Given an organizer who is also in the roster
When they open My assignment
Then they see their own pairing per event, read-only
```

## Dependencies & related

- Derived from: [UC-SS-011](../../use-cases/secret-santa/UC-SS-011-see-my-assignment.md)
- Related: [FR-041](FR-041-join-link-and-person-claim.md), [FR-032](FR-032-add-organizer-as-member.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
