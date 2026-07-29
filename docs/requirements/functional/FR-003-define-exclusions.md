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

## Dependencies & related

- Derived from: [UC-SS-003](../../use-cases/secret-santa/UC-SS-003-define-exclusions.md)
- Related: [FR-002](FR-002-manage-members-and-wishlists.md), [FR-004](FR-004-run-fair-draw.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md)
- Glossary: [Exclusion](../../glossary/exclusion.md), [Member](../../glossary/member.md), [Group](../../glossary/group.md), [Draw](../../glossary/draw.md)
