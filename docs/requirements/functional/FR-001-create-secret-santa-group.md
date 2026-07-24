---
id: FR-001
title: Create a Secret-Santa group offline
type: functional
status: Draft
priority: Must
source: UC-SS-001
verification: test
related: [UC-SS-001]
---

# FR-001 — Create a Secret-Santa group offline

## Description

The Secret-Santa app must let a user create a [Group](../../glossary/group.md) with a name and
persist it to local storage **without requiring an account or network connection**.

## Rationale

Local-first is a core quality goal (Q1): the app must be fully usable offline. Group creation is
the entry point to every other Secret-Santa flow.

## Acceptance criteria

```gherkin
Given the app is offline and the user has no account
When the user creates a group with a non-empty name
Then the group is saved to local storage
And it is immediately visible in the user's list of groups

Given the user tries to create a group with an empty name
When they attempt to save
Then saving is blocked and the user is prompted to enter a name
```

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md)
- Related quality goal: Q1 (local-first) — [arc42 ch. 1](../../arc42/01-introduction-and-goals.md)
