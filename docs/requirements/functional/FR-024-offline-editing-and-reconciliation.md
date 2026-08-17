---
id: FR-024
title: Offline editing and reconciliation for signed-in users
type: functional
status: Implemented
priority: Must
source: UC-MAIN-016
verification: test
related: [UC-MAIN-016, UC-MAIN-015, FR-023, NFR-001, NFR-008, ADR-0016, ADR-0012, ADR-0029, ADR-0044, FEAT-004]
---

# FR-024 — Offline editing and reconciliation for signed-in users

> **Extended by [ADR-0044](../../adr/0044-sign-in-reconciliation-and-account-scoped-visibility.md).**
> The reconnect / "pushed without the user asking" behaviour specified below is now actually wired,
> via an `IConnectivity` seam and an `AutoSyncService` that syncs **silently** on four triggers —
> sign-in, app start, every change (debounced), and reconnect. ADR-0044 also adds the **sign-in
> reconciliation** of offline data (Import / Discard / Cancel) and account-scoped visibility. The
> criteria below still hold.

## Description

Being signed in must never make the app less usable than being signed out. Every Secret-Santa flow —
create a [Group](../../glossary/group.md), edit members and [wishlists](../../glossary/wishlist.md),
set [exclusions](../../glossary/exclusion.md), run the [Draw](../../glossary/draw.md), generate
[reveal links](../../glossary/reveal-link.md) — works with no connection while signed in, and so does
changing the preferred language.

- A change made offline is written locally, bumps the aggregate's version, and is marked **not yet
  synced**. It is in full effect immediately.
- The app shows sync status — *synced* / *not synced* / *failed, retry* — as a fact, not an error,
  and offers a manual retry.
- On reconnect, pending aggregates are pushed and the delta is pulled. Push is **per aggregate** and
  **idempotent**: re-pushing what already arrived changes nothing and duplicates nothing.
- A delete made offline travels as a [tombstone](../../glossary/tombstone.md).
- Where the same aggregate changed on both sides since the cursor, **last-write-wins by
  server-recorded time**. One version survives whole; the other is discarded whole. **Every such
  collision emits a telemetry event**, whether or not anything is shown to the user.
- Queued changes survive a sign-out and are pushed after the same account signs in again on that
  device. They are never pushed to a different account.

## Rationale

The offline half is where a sync feature is actually judged. Trains, cellars and aeroplane mode are
normal, and a family app that loses an evening's work because the connection dropped is not one people
keep using.

Aggregate-level last-write-wins is deliberately the crudest strategy that converges
([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)): a field-wise merge would break the
aggregate's invariants and can produce a state no user ever created — a draw referring to a member the
other device removed. The accepted cost is that a genuinely concurrent edit is discarded.

The telemetry on every collision is not optional decoration. The conflict UI is parked *on the
evidence this hook produces*; a hook that is stubbed leaves its own design comment behind as false
reassurance, and we would conclude that collisions never happen because nobody was counting. M2 closed
with exactly that failure mode — a fitness function designed to be read back by a test that was never
written.

Not telling the user their version lost is a deliberate M3 choice: a message that says "your change
was replaced" and offers nothing to do about it is worse than the collision. It is revisited when the
telemetry shows how often this is real.

## Acceptance criteria

```gherkin
Given a signed-in user with no network
When they create a group, add members, set exclusions, run the draw and generate reveal links
Then every operation succeeds and is persisted locally

Given a signed-in user with no network
When they change their preferred language
Then the change applies immediately
And it is pushed on the next successful sync

Given an offline change marked not yet synced
When connectivity returns
Then it is pushed without the user asking
And the status clears

Given a push that has already been applied by the server
When it is pushed again after an interrupted sync
Then the server state is unchanged and no duplicate is created

Given a group deleted offline
When the device reconnects
Then a tombstone is pushed
And the group is gone on the user's other devices after their next sync

Given the same group edited on two devices while both were offline
When both reconnect
Then the version with the later server-recorded time survives in full
And the other is discarded in full
And a collision telemetry event is emitted

Given a push that fails for one aggregate
When the sync completes
Then the other aggregates are marked synced
And the failed one stays queued and is retried

Given a user who signs out with queued changes
When the same account signs in again on that device
Then the queued changes are pushed

Given a user who signs out with queued changes
When a different account signs in on that device
Then nothing queued is pushed to it

Given local storage that refuses a write
When the user makes a change
Then they are told the change was not saved
```

## Dependencies & related

- Derived from: [UC-MAIN-016](../../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md)
- Related: [FR-023](FR-023-sync-across-devices.md),
  [FR-012](FR-012-manage-profile.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md) — this requirement extends it from
  account-free mode to signed-in mode,
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Governing ADRs: [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) (LWW + escalation hook),
  [ADR-0012](../../adr/0012-observability-and-telemetry.md) (the hook must be observable),
  [ADR-0029](../../adr/0029-storage-write-failure-contract.md) (a refused write is reported)
- Implemented by: [FEAT-004](../../features/FEAT-004-m3-sync-and-claim.md) *(M3 — sync & claim; `Done`, built & verified 2026-08-12)*
- Glossary: [Sync](../../glossary/sync.md), [Tombstone](../../glossary/tombstone.md)
