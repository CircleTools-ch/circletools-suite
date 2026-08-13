---
id: NFR-008
title: Sync convergence & data safety
type: non-functional
status: Implemented
priority: Must
source: UC-MAIN-015, UC-MAIN-016, ADR-0016
verification: test
related: [UC-MAIN-015, UC-MAIN-016, UC-SS-007, UC-SS-008, FR-023, FR-024, FR-025, FR-026, NFR-001, NFR-002, ADR-0016, ADR-0012, FEAT-004]
---

# NFR-008 — Sync convergence & data safety

## Description

**[Sync](../../glossary/sync.md) must converge, and it must never lose data that the user can still
see.** Two properties, stated once for every aggregate that syncs:

- **Convergence.** Once every device has completed one sync cycle with no further edits, all of them
  hold the same set of aggregates at the same versions. A delete does not resurrect; a
  [tombstone](../../glossary/tombstone.md) that has been seen does not re-delete a later re-creation.
- **Data safety.** No local data is removed as a side effect of an account action. Signing in,
  [claiming](../../glossary/claim.md), un-claiming, signing out and deleting the account all leave the
  device's copy intact. The **only** thing that removes local data is the user deleting the group, on
  purpose, and its tombstone.

The single accepted exception is **last-write-wins**: when the same aggregate genuinely changed on
both sides since the client's cursor, the version with the earlier server-recorded time is discarded
in full. This is the one place data is lost, it is bounded to that case, and **every occurrence is
counted** in telemetry.

Sync is also never a gate: an edit is written locally and acknowledged to the user without waiting for
the network.

## Rationale

Convergence and data safety are the two properties a user actually notices, and both fail silently.
A group that quietly stops arriving on the second device, a delete that undeletes itself on the next
sync, an edit that disappears — none of these throw, and none is visible in a green test run unless a
test was written to look for exactly it.

Stating them once as an NFR rather than scattering them through
[FR-023](../functional/FR-023-sync-across-devices.md)–[FR-026](../functional/FR-026-remove-group-from-account.md)
follows the same reasoning as [NFR-005](NFR-005-account-enumeration-resistance.md) and
[NFR-006](NFR-006-circle-authorization.md): one property, one test approach, and one place to point at
when a new aggregate is made syncable.

Counting collisions matters more than resolving them prettily. The conflict UI is parked on the
evidence this counter produces ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)); if the
hook is a stub, we will conclude that collisions never happen because nobody counted. That is exactly
how M2 shipped a fitness function that existed only as a design comment.

## Acceptance criteria

```gherkin
Given two devices holding the same account's data
When both complete one sync cycle with no further edits
Then they hold the same aggregates at the same versions

Given a group deleted on one device
When every other device has completed a sync cycle
Then the group is absent everywhere
And it does not reappear on any subsequent cycle

Given a sequence of pushes replayed after an interrupted sync
When the server state is inspected
Then it is identical to the state after a single clean push

Given any account action — sign-in, claim, un-claim, sign-out, account deletion
When it completes
Then the number of groups in local storage is unchanged

Given the same aggregate edited concurrently on two devices
When the sync reconciles them
Then exactly one version survives in full
And a collision event is recorded in telemetry

Given a device that has been offline with pending changes
When connectivity returns
Then all pending aggregates are pushed within one sync cycle

Given a user editing while offline
When the edit is saved
Then no network call is awaited before the user is told it is saved
```

## Quality attribute *(NFR only)*

- **Attribute:** reliability / data integrity (convergence, durability), with a performance floor.
- **Metric & target:**
  - **convergence:** 100 % of two-device scenarios converge within **one** sync cycle after the last
    edit; a family-scale account (**≤ 200 aggregates**) completes a cycle in **≤ 5 s** on a normal
    connection;
  - **local data loss: zero** — no account action reduces the number of locally stored groups;
  - **server data loss: zero** outside last-write-wins; LWW discards are **100 % counted** in
    telemetry, with aggregate type and id;
  - **idempotency: zero** duplicate aggregates after any number of replayed pushes;
  - **delete propagation: zero** resurrections after a tombstone has been seen by a device;
  - **no-op cost:** a cycle with no changes transfers **no aggregate bodies**;
  - **write latency:** a local edit is acknowledged with **zero** awaited network calls.
- **Measurement approach:** a two-client integration harness that drives real sync cycles against the
  API (offline edits, concurrent edits, deletes, replayed pushes) and asserts the end states;
  a local-storage count assertion around each account action; a test asserting a collision emits its
  telemetry event; a payload assertion on the no-change cycle; timing measured on a seeded
  200-aggregate account.

## Dependencies & related

- Derived from: [UC-MAIN-015](../../use-cases/main/UC-MAIN-015-sync-across-devices.md),
  [UC-MAIN-016](../../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md) — it constrains all
  four M3 requirements
- Related: [NFR-001](NFR-001-local-first-offline.md) (the property sync must not weaken),
  [NFR-002](NFR-002-assignment-privacy.md) (what the uploaded aggregate contains),
  [FR-023](../functional/FR-023-sync-across-devices.md),
  [FR-024](../functional/FR-024-offline-editing-and-reconciliation.md),
  [FR-025](../functional/FR-025-claim-local-groups.md),
  [FR-026](../functional/FR-026-remove-group-from-account.md)
- Governing ADRs: [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) (the mechanism and its
  accepted data-loss window), [ADR-0012](../../adr/0012-observability-and-telemetry.md) (the hook must
  be observable), [ADR-0029](../../adr/0029-storage-write-failure-contract.md)
- Implemented by: [FEAT-004](../../features/FEAT-004-m3-sync-and-claim.md) *(M3 — sync & claim; `Done`, built & verified 2026-08-12)*
- Glossary: [Sync](../../glossary/sync.md), [Tombstone](../../glossary/tombstone.md),
  [Claim](../../glossary/claim.md)
