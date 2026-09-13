---
id: NFR-010
title: Shared-by-link groups have a defined retention & deletion lifecycle
type: non-functional
status: Draft
priority: Must
source: FR-039
verification: test
related: [FR-039, FR-043, NFR-001, NFR-009, ADR-0058, ADR-0022]
---

# NFR-010 — Shared-by-link groups have a defined retention & deletion lifecycle

## Description

A [shared-by-link group](../../glossary/shared-by-link-group.md) lives on the server with **no account
owner** to manage or delete it ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)). Owner-less
server data cannot accumulate forever, so the mode has an explicit lifecycle:

- **Explicit delete.** Any [admin link](../../glossary/admin-link.md) holder can delete the online copy
  (the "stop sharing / make on-this-device only" path), which removes the server copy — **not** a
  [tombstone](../../glossary/tombstone.md), the same as removing a group from an account
  ([FR-043](../functional/FR-043-release-ownership-and-deletion-disposition.md)).
- **Inactivity expiry.** A shared group with no reads or writes for a defined retention window is
  **automatically expired** (its server copy purged). The window is generous and documented, and expiry
  is what stops abandoned groups living on our storage indefinitely.
- **Local copy is never destroyed by the server.** Expiry or online-delete removes only the **server**
  copy; a device still holding the group keeps its local copy
  ([NFR-001](NFR-001-local-first-offline.md)) — deleting online is a downgrade to on-this-device for
  anyone who has it locally, never data loss on the device.
- **Warned, not surprised.** Where a device can be reached, an approaching expiry is surfaced so an admin
  can keep the group alive (any activity resets the window).

This is the data-protection counterpart, for owner-less groups, of the account-scoped deletion in
[ADR-0022](../../adr/0022-account-deletion-and-data-export.md); the anti-abuse controls are
[NFR-009](NFR-009-account-free-write-abuse-resistance.md).

## Rationale

Storing groups keyed by a link rather than an account means no one is responsible for cleaning them up.
Without a retention rule, abandoned shared groups would grow without bound and hold personal data
(rosters, wishlists, notes) far past its usefulness — a cost and a privacy liability. A generous
inactivity expiry plus an always-available explicit delete bounds both, while never touching a device's
local copy keeps the local-first promise intact in both directions.

## Acceptance criteria

```gherkin
Given a shared-by-link group and an admin-link holder
When they delete the online copy
Then the server copy is removed (no tombstone) and its links stop resolving
And a device that held the group locally still has its local copy

Given a shared-by-link group with no reads or writes for the retention window
When the window elapses
Then the server copy is purged automatically

Given a shared group approaching its expiry and a reachable device
When the group is opened
Then the impending expiry is surfaced, and any activity resets the window

Given a shared group is expired or deleted online
When a device that held it opens the app
Then the group is present locally as an on-this-device group and no data is lost on the device
```

## Quality attribute *(NFR only)*

- **Attribute:** reliability / retention / data-protection.
- **Metric & target:**
  - **100%** of shared groups inactive beyond the retention window are purged from the server;
  - **zero** cases where server expiry or online-delete removes a device's **local** copy;
  - explicit online-delete removes the server copy in a single action available to every admin-link
    holder.
- **Measurement approach:** integration tests advancing time past the window and asserting purge;
  local-first tests asserting the device copy survives server expiry/delete; an API test that an
  admin-link holder can delete the online copy and its links then 404-equivalently.

## Dependencies & related

- Derived from: [FR-039](../functional/FR-039-shared-by-link-storage-and-sync.md)
- Related: [FR-043](../functional/FR-043-release-ownership-and-deletion-disposition.md) (release &
  online-delete), [NFR-009](NFR-009-account-free-write-abuse-resistance.md) (abuse resistance),
  [NFR-001](NFR-001-local-first-offline.md) (local copy survives)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md); data-protection
  counterpart to [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- Verified by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md) /
  [TP-013](../../features/TP-013-account-free-link-shared-groups.md)
- Glossary: [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Tombstone](../../glossary/tombstone.md)
