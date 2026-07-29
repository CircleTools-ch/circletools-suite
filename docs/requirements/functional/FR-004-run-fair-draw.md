---
id: FR-004
title: Run a fair draw with rules, history, and graceful failure
type: functional
status: Implemented
priority: Must
source: UC-SS-004
verification: test
related: [UC-SS-004, FR-003, FR-005, NFR-001, NFR-002]
---

# FR-004 — Run a fair draw with rules, history, and graceful failure

## Description

The Secret-Santa app must run a random [Draw](../../glossary/draw.md) that produces exactly one
[Assignment](../../glossary/assignment.md) per [Member](../../glossary/member.md) — every member
gives once and receives once — while enforcing all of the following rules:

1. **Honor exclusions** — no assignment may violate a defined [Exclusion](../../glossary/exclusion.md).
2. **No reciprocal pairs** — if A gives to B, then B must not give to A.
3. **No repeat of the previous draw** — no giver may keep the same recipient as in the group's last
   recorded draw.

The app must keep a **draw history** so rule (3) can be enforced, record each successful draw as the
new current draw, and **fail gracefully** when no valid assignment exists — producing no partial
result and explaining the likely cause (over-constrained). The draw runs entirely on-device
**without an account or network connection**.

## Rationale

A fair, valid, non-repeating draw is the heart of the product. Enforcing the three rules and
keeping history makes results feel fair year over year; graceful failure prevents silently invalid
or partial draws.

## Acceptance criteria

```gherkin
Given a group with enough members and satisfiable constraints
When the user runs the draw
Then every member is assigned exactly one recipient and appears as a recipient exactly once
And no assignment violates an exclusion
And no two members are assigned to each other (no reciprocal pair)

Given the group has a previous recorded draw
When the user runs a new draw
Then no giver keeps the same recipient as in the previous draw
And the new draw is recorded as current while the previous draw is retained in history

Given exclusions, the no-reciprocal rule, and the no-repeat rule together admit no valid assignment
When the user runs the draw
Then the draw fails gracefully with no assignments saved
And the user is shown the likely cause and asked to relax constraints or add members

Given a group with exactly two members
When the user runs the draw
Then the system explains that a valid non-reciprocal draw is impossible and produces no result
```

## Dependencies & related

- Derived from: [UC-SS-004](../../use-cases/secret-santa/UC-SS-004-run-draw.md)
- Related: [FR-003](FR-003-define-exclusions.md), [FR-005](FR-005-generate-reveal-links.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Glossary: [Draw](../../glossary/draw.md), [Assignment](../../glossary/assignment.md), [Exclusion](../../glossary/exclusion.md), [Member](../../glossary/member.md), [Group](../../glossary/group.md)
