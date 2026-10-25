---
id: FR-043
title: Ownership can move between an account and an organizer link, and account deletion asks about owned groups
type: functional
status: Implemented
priority: Must
source: UC-SS-017, UC-MAIN-006
verification: test
related: [UC-SS-017, UC-SS-018, UC-MAIN-006, FR-038, FR-040, FR-015, ADR-0086, ADR-0022, FEAT-027]
---

# FR-043 — Ownership can move between an account and an organizer link, and account deletion asks about owned groups

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

An online group's [owner](../../glossary/owner.md) can change without losing the group or its participants:

- **Take over** — signed in on a device that holds a group's [organizer link](../../glossary/organizer-link.md), the user can
  keep the group in their account. The account becomes the owner; the organizer link stops working; the join link
  and every participant are unaffected.
- **Hand over** — the owning account can hand the group to a fresh organizer link, for example so several people can
  organize it without accounts. The account stops owning it.

**Account deletion** ([FR-015](FR-015-delete-account.md)): if the account owns online groups, the delete screen
says how many and asks what becomes of them —

- **keep them online** *(default)*: each group is handed over to a new organizer link, so its participants keep
  access;
- **delete them too**: their online copies are deleted.

Either way every other role the account held (participant, invitation) is removed; what it wrote stays in those
groups.

## Rationale

Ownership must never be a trap in either direction: signing up should not cost a group its participants, and
leaving should not strand everyone else (ADR-0086, ADR-0022).

## Acceptance criteria

```gherkin
Given a signed-in user on a device holding a group's organizer link
When they keep the group in their account and confirm
Then the account owns the group, the organizer link no longer works, and the join link still does

Given an account owns an online group
When it hands the group over and confirms
Then a fresh organizer link owns the group and the account no longer does

Given an account that owns one online group
When the user opens account deletion
Then the screen says they organize one online group and offers to keep it online or delete it

Given the user deletes the account keeping its groups online
When someone opens a group's join link afterwards
Then the group is still there
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md),
  [UC-MAIN-006](../../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md), [FR-040](FR-040-admin-link-capability.md), [FR-015](FR-015-delete-account.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md); [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
