---
id: FR-040
title: The organizer link lets devices organize an online group without an account
type: functional
status: Implemented
priority: Must
source: UC-SS-018
verification: test
related: [UC-SS-018, FR-038, FR-039, FR-041, FR-043, NFR-009, ADR-0086, FEAT-027]
---

# FR-040 — The organizer link lets devices organize an online group without an account

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

An [online group](../../glossary/online-group.md) created without an account is owned by its
[organizer link](../../glossary/organizer-link.md) (decision D1 of ADR-0086):

- The link carries the group id and an unguessable token (`/admin#g=…&t=…`). Every device that opens it becomes an
  organizer of the group — which is also how organizers share the work.
- The device that created the group keeps the link and shows it in *Sharing & sync*, with Copy, QR code and
  share actions.
- The owner can **replace** it with a fresh link; the old one stops working at once.
- When an account **takes over** the group ([FR-043](FR-043-release-ownership-and-deletion-disposition.md)), the
  organizer link stops working. The [join link](../../glossary/join-link.md) is unaffected by either.
- The server stores the token only as a hash (and an encrypted copy, so the owner's devices can show the link
  again); the token travels in a request header, never in a URL path.

## Rationale

Organizing without signing up is part of the product's promise (decision D1). A link the organizers share keeps
that working while making it one more kind of owner in the same role model, rather than a separate store.

## Acceptance criteria

```gherkin
Given a signed-out user puts a group online
When the group is created online
Then an organizer link owns it and Sharing & sync shows the link

Given a second device opens the organizer link
When the link is valid
Then that device lands in the group as an organizer, and its edits reach the first device

Given the owner replaces the organizer link
When the old link is opened
Then it no longer gives access

Given an account takes the group over
When the old organizer link is opened
Then it no longer gives access, and the join link still works
```

## Dependencies & related

- Derived from: [UC-SS-018](../../use-cases/secret-santa/UC-SS-018-share-and-manage-as-admin.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md), [FR-039](FR-039-shared-by-link-storage-and-sync.md),
  [FR-043](FR-043-release-ownership-and-deletion-disposition.md),
  [NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary: [Organizer link](../../glossary/organizer-link.md), [Owner](../../glossary/owner.md)
