---
id: FR-024
title: Edits made offline are kept and sent when the connection returns
type: functional
status: Implemented
priority: Must
source: UC-MAIN-016
verification: test
related: [UC-MAIN-016, UC-MAIN-015, FR-023, FR-039, NFR-001, NFR-008, ADR-0086, ADR-0016, ADR-0029, FEAT-027]
---

# FR-024 — Edits made offline are kept and sent when the connection returns

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

Every group stays usable offline, and nothing typed offline is lost:

- **Groups on this device** never need a connection.
- **An organizer's edits to an online group** are saved on the device and marked unsent. The next sync pushes them
  before it reads the group back, so an offline edit is not replaced by the older server copy. If two organizers
  edited the same group, the later push wins in full (last write wins per group,
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)); members' own entries are kept either way
  ([FR-039](FR-039-shared-by-link-storage-and-sync.md)).
- **A participant's wishlist and gift note** written offline are kept as pending and sent on the next sync; the
  page says they are saved on the device until then.
- **Profile and preferences** edited offline are sent on the next account sync.

If the device's storage refuses a write, the user is told, and what the server holds is not changed on that
device's account ([ADR-0029](../../adr/0029-storage-write-failure-contract.md)).

## Rationale

Gift exchanges are planned on trains and in kitchens with bad reception. Local-first only holds if an offline
edit survives the next sync instead of being overwritten by it (NFR-001, NFR-008).

## Acceptance criteria

```gherkin
Given an organizer edits an online group while offline
When the connection returns and the device syncs
Then the edit reaches the server and is still on the device afterwards

Given a participant saves their wishlist while offline
When the connection returns
Then the wishlist is sent and the giver sees it

Given an organizer device holds an unsent edit
When it syncs
Then it pushes before it reads, and the unsent edit is not replaced by the server copy
```

## Dependencies & related

- Derived from: [UC-MAIN-016](../../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md)
- Related: [FR-023](FR-023-sync-across-devices.md), [FR-039](FR-039-shared-by-link-storage-and-sync.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
