---
id: NFR-010
title: Online groups without an account owner have a defined lifecycle
type: non-functional
status: Implemented
priority: Must
source: FR-039
verification: test
related: [FR-039, FR-043, NFR-001, NFR-009, ADR-0086, ADR-0022]
---

# NFR-010 — Online groups without an account owner have a defined lifecycle

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

An [online group](../../glossary/online-group.md) owned by an [organizer link](../../glossary/organizer-link.md) has **no account** behind
it to manage or delete it. Such groups cannot accumulate forever:

- **Explicit delete.** Any device holding the organizer link can keep the group only on that device, which deletes
  the online copy (no tombstone) and stops its links.
- **Inactivity expiry.** A group no account owns that nobody has used for **365 days**
  (`SharedGroups:RetentionDays`) is deleted by a daily sweep. Every read and every write counts as use.
- **The device's copy is never destroyed by the server.** Expiry or delete removes only the online copy; an
  organizer's device that still holds the group keeps it as a group on that device
  ([NFR-001](NFR-001-local-first-offline.md)).

A group an **account** owns follows the account's lifecycle instead: it stays while the account exists, and account
deletion hands it over or deletes it as the user chose
([FR-043](../functional/FR-043-release-ownership-and-deletion-disposition.md),
[ADR-0022](../../adr/0022-account-deletion-and-data-export.md)).

## Rationale

Groups keyed by a link rather than an account have nobody responsible for cleaning them up. Without a rule,
abandoned groups would hold personal data (rosters, wishlists, notes) long past their use — a cost and a privacy
liability. A generous inactivity window plus an always-available delete bounds both.

## Acceptance criteria

```gherkin
Given an online group no account owns
When a device holding its organizer link keeps it only on that device
Then the online copy is deleted and its links stop working, and that device keeps its copy

Given an online group no account owns, unused for 365 days
When the daily sweep runs
Then the group is deleted

Given such a group was read once within the window
When the sweep runs
Then the group is kept

Given an online group an account owns, unused for 365 days
When the sweep runs
Then the group is kept
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / data minimization.
- **Metric & target:** no group without an account owner older than 365 days since its last use.
- **Measurement approach:** integration test of the sweep (unused group deleted, used and owned groups kept).

## Dependencies & related

- Related: [FR-039](../functional/FR-039-shared-by-link-storage-and-sync.md),
  [FR-043](../functional/FR-043-release-ownership-and-deletion-disposition.md), [NFR-009](NFR-009-account-free-write-abuse-resistance.md)
- Governing ADRs: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md), [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
