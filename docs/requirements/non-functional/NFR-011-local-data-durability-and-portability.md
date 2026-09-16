---
id: NFR-011
title: Local data is durable and portable (web)
type: non-functional
status: Implemented
priority: Should
source: FEAT-017
verification: test
related: [NFR-001, NFR-008, NFR-004, FEAT-017, TP-017, ADR-0026, ADR-0048]
---

# NFR-011 — Local data is durable and portable (web)

> **Scope: the web head.** The MAUI heads store group data in native app storage, which is durable and
> not evictable, so this requirement is satisfied there by construction. It exists because the **browser**
> is the head whose only copy lives in evictable, invisible web storage.

## Description

[NFR-001](NFR-001-local-first-offline.md) requires the app to *work* offline with no account, holding all
state in local storage. This requirement adds two qualities of that local storage on the web head, which
NFR-001 does not cover:

- **Durability.** A group in the browser's `localStorage` must be protected against **automatic** loss as
  far as the platform allows. The app requests persistent storage (`navigator.storage.persist()`) so the
  browser will not evict the data under storage pressure, and it reports the current grant
  (`navigator.storage.persisted()`) to the user in plain language, so the user is never falsely reassured.
  A write the store **refuses** is surfaced, never silently swallowed — extending
  [NFR-008](NFR-008-sync-convergence-and-data-safety.md)'s "no silent data loss" from the sync path to the
  bare local write.
- **Portability.** A user must be able to get their data **out and back** without an account or a server:
  export every group to a self-describing, versioned backup file and restore it later — on the same
  browser after a wipe, or on a different browser or device. Restore **merges** (adds new groups,
  overwrites a same-id group with the backed-up copy) and **never deletes** a group already on the device;
  a file that is not a backup, or one written by a newer app version, is **rejected safely** rather than
  mis-read.

Durability is best-effort where the platform makes it so (a user can always clear their own browser data,
and browsers grant persistence unevenly); portability is the user-controlled safety net that does not
depend on the browser's cooperation. Neither introduces an account or a backend, so
[NFR-001](NFR-001-local-first-offline.md) is upheld.

## Rationale

On the web the entire data set can vanish in ways the user neither sees nor intends — browser eviction
under storage pressure, a 7-day inactivity wipe on Safari/ITP, a private window, or a "clear browsing
data" click. For a Secret-Santa group that took real effort to build, that is silent, unrecoverable loss.
Requesting persistence removes the *accidental* causes as far as the platform allows; a downloadable
backup makes the data tangible and recoverable regardless; and reporting the persistence state honestly
stops the UI from implying a safety it does not have. This is the local-first counterpart, for the
account-free web user, of the account holder's server-side durability and the export in
[ADR-0022](../../adr/0022-account-deletion-and-data-export.md); it also serves the data-protection posture
of [NFR-004](NFR-004-data-protection-and-retention.md) by keeping the user in control of their own copy.

## Acceptance criteria

```gherkin
Given the web app is opened
When it starts up
Then it requests persistent storage, and the settings screen reports whether the browser has granted it

Given a set of groups held only in this browser
When the user exports a backup and then clears local storage
And later imports that backup file
Then every group is restored intact (members, events, wishlists, exclusions, draws)

Given a device that already holds some groups
When the user imports a backup containing a new group and an updated copy of an existing group
Then the new group is added and the existing one is overwritten
And a group on the device that is absent from the backup is left untouched

Given a file that is not a Secret-Santa backup, or one written by a newer app version
When the user tries to restore it
Then the import is refused with a clear message and nothing on the device is changed

Given local storage refuses a write (blocked or full)
When a group is saved
Then the failure is surfaced to the user rather than silently swallowed
```

## Quality attribute *(NFR only)*

- **Attribute:** reliability / durability / data-protection (data safety & portability).
- **Metric & target:**
  - persistent storage is **requested** at startup and its current state is **surfaced** to the user;
  - **100%** of a device's groups round-trip through export → import with no loss of content;
  - import **never** deletes a pre-existing group; a foreign or newer-version file writes **nothing**;
  - a **refused** local write is reported, never swallowed (zero silent local-write losses).
- **Measurement approach:** `BackupServiceTests` (round-trip, merge-by-id, reject foreign/malformed,
  refuse newer version, skip a corrupt entry) and `GroupRepositoryTests` (a refused write throws); a
  browser walkthrough per [TP-017](../../features/TP-017-local-data-transparency-and-backup.md) covering
  `persist()`/`persisted()`, export, wipe-and-restore, and the invalid-file path.

## Dependencies & related

- Extends: [NFR-001](NFR-001-local-first-offline.md) (local-first / offline) and
  [NFR-008](NFR-008-sync-convergence-and-data-safety.md) (no silent data loss)
- Related: [NFR-004](NFR-004-data-protection-and-retention.md) (data protection & retention),
  [ADR-0026](../../adr/0026-refresh-token-persistence.md) (durable vs session web storage — the
  stay-signed-in distinction),
  [ADR-0048](../../adr/0048-web-app-update-delivery.md) (new-version prompt — the other half of the same
  "data on the web" concern)
- Verified by: [FEAT-017](../../features/FEAT-017-local-data-transparency-and-backup.md) /
  [TP-017](../../features/TP-017-local-data-transparency-and-backup.md)
