---
id: FR-023
title: An account's groups and preferences reach all its devices
type: functional
status: Implemented
priority: Must
source: UC-MAIN-015
verification: test
related: [UC-MAIN-015, UC-MAIN-016, FR-012, FR-024, FR-039, NFR-001, NFR-002, NFR-008, ADR-0086, ADR-0016, ADR-0033, ADR-0041, FEAT-027]
---

# FR-023 — An account's groups and preferences reach all its devices

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

A signed-in user sees the same groups on every device:

- **Online groups** ([FR-039](FR-039-shared-by-link-storage-and-sync.md)) — every group the account owns, every group it takes part in, and every invitation
  waiting for an answer. A device discovers them from the server on each sync and keeps a copy, so they also open
  offline. A group the account no longer has a role in (deleted, left, freed, handed over) disappears from the
  device.
- **The account documents** — profile (display name, language) and UI preferences (pinned groups) — travel through
  the account sync ([ADR-0041](../../adr/0041-sync-secret-santa-ui-preferences.md)).

Groups kept **on this device** are never sent anywhere, signed in or not
([NFR-001](../non-functional/NFR-001-local-first-offline.md)). A group of one account is never shown while another
account is signed in on the same device.

Sync runs on app start, after a change, when the connection returns, and on request; it needs no action from the
user.

## Rationale

People switch between phone and laptop. What they organize and what they take part in should be there on each,
without exporting or re-entering anything (ADR-0086, ADR-0016).

## Acceptance criteria

```gherkin
Given an account owns an online group created on its phone
When the account's laptop syncs
Then the group appears on the laptop

Given the account deletes that group online on the phone
When the laptop syncs
Then the group disappears from the laptop

Given an account accepts an invitation on one device
When its other device syncs
Then the group appears there among the groups it takes part in

Given a group kept on this device
When the user is signed in and the device syncs
Then nothing of the group is sent to the server

Given the user pins a group on one device
When another device of the account syncs
Then the group is pinned there too
```

## Dependencies & related

- Derived from: [UC-MAIN-015](../../use-cases/main/UC-MAIN-015-sync-across-devices.md)
- Related: [FR-024](FR-024-offline-editing-and-reconciliation.md), [FR-039](FR-039-shared-by-link-storage-and-sync.md),
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md); [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md), [ADR-0033](../../adr/0033-which-aggregates-sync.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary: [Sync](../../glossary/sync.md), [Online group](../../glossary/online-group.md)
