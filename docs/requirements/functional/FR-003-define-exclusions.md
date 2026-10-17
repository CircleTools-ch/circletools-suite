---
id: FR-003
title: Define exclusions between members
type: functional
status: Implemented
priority: Must
source: UC-SS-003
verification: test
related: [UC-SS-003, FR-002, FR-004, NFR-001]
---

# FR-003 — Define exclusions between members

## Description

The Secret-Santa app must let a user define and remove [Exclusions](../../glossary/exclusion.md)
within a [Group](../../glossary/group.md). An exclusion is **one-directional**: "giver X must not
give to recipient Y". The app must support adding both directions when a mutual exclusion is
wanted, and must persist exclusions to local storage **without an account or network connection**.

## Rationale

Exclusions let organizers encode real-life constraints (partners, same household) so the draw
avoids unwanted pairings. They are a required input to the fair draw ([FR-004](FR-004-run-fair-draw.md)).

## Acceptance criteria

```gherkin
Given a group with at least two members
When the user adds an exclusion from giver X to recipient Y
Then a one-directional exclusion X→Y is saved to the group

Given the user chooses a mutual exclusion between X and Y
When they confirm
Then both X→Y and Y→X exclusions are saved

Given the user selects the same member as giver and recipient
When they attempt to save the exclusion
Then it is rejected because a member never gives to themselves

Given an exclusion X→Y already exists
When the user adds the same exclusion again
Then the duplicate is ignored
```

## Amendment (2026-10-16) — warn while editing

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q13):

- [ ] The exclusions page warns **as soon as** the exclusions leave someone with nobody to give to, or
      nobody who may give to them, naming the person; or, when nobody is stuck on their own, that together
      they rule out any draw (the no-reciprocal rule included). Saving them stays allowed.
- [ ] A drawn occasion whose current draw pairs someone a **later** exclusion forbids says so — on its card,
      its hub tile and the draw page ("no longer matches the exclusions — draw again?").
- [ ] After a failed re-draw the draw page keeps showing the existing draw (it still applies), with the error
      above it.

## Dependencies & related

- Derived from: [UC-SS-003](../../use-cases/secret-santa/UC-SS-003-define-exclusions.md)
- Related: [FR-002](FR-002-manage-members-and-wishlists.md), [FR-004](FR-004-run-fair-draw.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md)
- Glossary: [Exclusion](../../glossary/exclusion.md), [Member](../../glossary/member.md), [Group](../../glossary/group.md), [Draw](../../glossary/draw.md)
