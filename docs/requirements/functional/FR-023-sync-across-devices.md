---
id: FR-023
title: Sync account-scoped data across a user's devices
type: functional
status: Approved
priority: Must
source: UC-MAIN-015
verification: test
related: [UC-MAIN-015, UC-MAIN-016, UC-SS-007, UC-SS-008, FR-012, FR-024, FR-025, FR-026, NFR-001, NFR-002, NFR-008, ADR-0016, ADR-0020, ADR-0033, FEAT-004]
---

# FR-023 — Sync account-scoped data across a user's devices

## Description

A signed-in [User](../../glossary/user.md) must see the same account-scoped data on every device they
sign in on, without any manual transfer.

**What syncs in M3:**

- Secret-Santa [Groups](../../glossary/group.md) the user has
  [claimed](../../glossary/claim.md) into their account — the whole aggregate: members,
  [wishlists](../../glossary/wishlist.md), [exclusions](../../glossary/exclusion.md),
  [event details](../../glossary/event-details.md), and the [Draw](../../glossary/draw.md) with its
  [assignments](../../glossary/assignment.md) if one has run;
- the profile preferences from [FR-012](FR-012-manage-profile.md) — display name and preferred
  language.

**What does not sync in M3:** [Circle](../../glossary/circle.md) data. Circles stay
server-authoritative and online-only as M2 built them — this narrows
[ADR-0016](../../adr/0016-sync-and-conflict-resolution.md), now recorded as
[ADR-0033](../../adr/0033-which-aggregates-sync.md).

**The mechanism**, per ADR-0016:

- the **aggregate is the unit** — never a field-wise merge;
- each aggregate carries a **version token**, bumped on every local change;
- the client keeps a **cursor** — the server state it last saw — and exchanges only the delta;
- deletes travel as [**tombstones**](../../glossary/tombstone.md);
- sync runs on **app start**, **on change**, and **when connectivity returns**, plus a manual retry
  from the sync-status surface;
- ordering uses **server-recorded** time, never device time.

The local copy is always written first and is always usable. Sync is a relay and a backup; it is
never in the write path of an edit.

## Rationale

Sync is the reason the account exists. M1 and M2 gave a user a name, a password and a circle; nothing
they actually make in the app moved between their own devices. Until M3, signing in bought less than
not signing in.

Writing locally first and syncing after is what keeps
[NFR-001](../non-functional/NFR-001-local-first-offline.md) true for signed-in users too: the
alternative — an edit that waits for a round trip — would make the account a downgrade, which is
exactly the trade this product refuses to make.

Server-recorded time rather than device time contains clock skew, at the accepted cost that a purely
offline reorder is decided by arrival order rather than by when the user typed it.

## Acceptance criteria

```gherkin
Given a user with a claimed group on device A
When they sign in on device B and the app starts
Then the group appears on device B with the same members, wishlists, exclusions and draw

Given a user signed in on two devices
When they change a group on device A
Then device B holds the change after its next sync

Given a user signed in on two devices
When they change their preferred language on device A
Then device B applies that language after its next sync

Given a group deleted on device A
When device B syncs
Then the group is removed on device B
And it does not reappear on any later sync

Given a sync in which nothing changed on either side
When the exchange is inspected
Then no aggregate bodies are transferred

Given an access token that expires during a sync
When the sync continues
Then it is refreshed transparently and the sync completes

Given a refresh that fails
When the sync stops
Then the user is signed out
And no local data is removed

Given a user who signs out
When they continue using the app
Then their local copy remains fully usable
And nothing is sent to the server

Given a circle
When the device is offline
Then circle screens state that they need a connection
And no circle data is written locally for later push

Given an account that is deleted
When the deletion completes
Then every server-side copy of that account's groups is gone
And the local copy on each device remains as account-free data
```

## Dependencies & related

- Derived from: [UC-MAIN-015](../../use-cases/main/UC-MAIN-015-sync-across-devices.md)
- Related: [FR-024](FR-024-offline-editing-and-reconciliation.md) (the offline half),
  [FR-025](FR-025-claim-local-groups.md) (what gets an account scope in the first place),
  [FR-026](FR-026-remove-group-from-account.md),
  [FR-012](FR-012-manage-profile.md) — **this requirement closes FR-012's carried criterion**, the
  offline language change pushed on the next sync,
  [FR-015](FR-015-delete-account.md) (server copies go, local copies stay),
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Governing ADRs: [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) (unit, cursor,
  tombstones, LWW, `localStorage` retained), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md),
  [ADR-0008](../../adr/0008-entity-id-strategy.md)
- **Resolved architectural decision:** ADR-0016 named the **circle** as a sync unit; this requirement
  excludes it. Last-write-wins is defensible for an aggregate one account owns and wrong for one that
  four people administer, and an offline circle write cannot take the server-side authorization
  decision [NFR-006](../non-functional/NFR-006-circle-authorization.md) requires. The narrowing is now
  recorded as [ADR-0033](../../adr/0033-which-aggregates-sync.md), which also parks an offline circle
  *read* cache as a separate, later decision.
- Implemented by: [FEAT-004](../../features/FEAT-004-m3-sync-and-claim.md) *(M3 — sync & claim; Draft, not yet built)*
- Glossary: [Sync](../../glossary/sync.md), [Tombstone](../../glossary/tombstone.md),
  [Claim](../../glossary/claim.md), [Group](../../glossary/group.md)
