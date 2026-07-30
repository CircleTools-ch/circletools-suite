---
id: UC-MAIN-016
title: Work offline while signed in, and reconcile when back online
app: Main
actors: [User]
status: Approved
priority: Must
related: [FR-024, NFR-001, NFR-008, ADR-0016]
---

# UC-MAIN-016 — Work offline while signed in, and reconcile when back online

## Summary / goal

A signed-in [User](../../glossary/user.md) on a train, in a cellar, or with the aeroplane mode still
on keeps working exactly as they do online — and when the connection returns, what they did travels
to the server without being asked to. Signing in must not make the app *less* usable than it is with
no account at all.

## Primary actor & stakeholders

- **Primary actor:** a signed-in user with no usable connection.
- **Stakeholders & interests:** the user, whose only copy of a change is on this device until it is
  pushed; anyone editing the same group on another device, whose work must not be silently thrown
  away.

## Preconditions

- The actor is signed in; the device holds account-scoped data.
- The device is offline, or the server is unreachable.

## Postconditions

- **Success:** every offline change is on the server, and both devices hold the same version of every
  aggregate.
- **Failure:** the change is still on the device, still visible to the user, and still queued. Nothing
  is lost, and the user is told the data is not yet on the server.

## Trigger

The actor edits something while the device cannot reach the server; or connectivity returns while
queued changes exist.

## Main success scenario

1. Actor creates or edits a [Group](../../glossary/group.md) — or changes their preferred language —
   with no connection.
2. System writes it to local storage, bumps the aggregate's version, and marks it as
   **not yet synced**. The change is fully in effect locally: a draw can be run, links generated.
3. System shows an unobtrusive *not synced* indication — a fact, not an error.
4. Connectivity returns. System pushes every pending aggregate and pulls what changed on the server.
5. The indication clears. Both devices converge.

## Extensions / alternate flows

- **1a. The actor deletes a group offline** → the delete becomes a
  [tombstone](../../glossary/tombstone.md) and is pushed like any other change. Without the tombstone
  the group would be re-sent by the other device and reappear.
- **4a. The same aggregate was also changed on the server since this device's cursor** → genuine
  concurrent edit. **Last-write-wins by server-recorded time**: one version survives whole, the other
  is discarded whole. Aggregates are never merged field by field — a group's members, exclusions and
  draw are one consistent set, and a field-wise merge can produce a state no user ever created (a draw
  pointing at a member the other device removed).
- **4b. A collision as in 4a occurs** → the reconciliation point raises it as telemetry
  ([ADR-0012](../../adr/0012-observability-and-telemetry.md)), always, whether or not the user is
  shown anything. This is the only way we ever find out whether collisions are real, and therefore
  whether a conflict UI is worth building — it is parked precisely on that evidence
  ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)).
- **4c. The push partially fails** → the aggregates that succeeded are marked synced, the rest stay
  queued. Sync is per-aggregate and idempotent: re-pushing what already arrived changes nothing and
  duplicates nothing.
- **4d. The actor signed out while offline** → queued changes stay in local storage and are pushed if
  the same account signs in again on this device. They are never pushed to a *different* account.

## Exceptions

- **The connection returns and drops again mid-sync** → whatever was pushed is pushed; the rest stays
  queued. There is no partial-aggregate state, because the aggregate is the unit.
- **The refresh token has expired while offline** → on reconnect the actor is signed out and the
  queue is kept. It is pushed after the next successful sign-in on this device, not discarded.
- **Local storage refuses the write** → the change did not happen and the user is told
  ([ADR-0029](../../adr/0029-storage-write-failure-contract.md)). Reporting a change as saved when it
  was not is the one failure this design cannot recover from.

## Non-functional notes

- **Offline/local-first:** this use case *is*
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md), extended to the
  signed-in case: every Secret-Santa flow works offline whether or not an account exists.
- **Localization:** the sync-status wording is DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)) and must read as
  reassurance ("saved on this device") rather than alarm.
- **Privacy:** unchanged from
  [UC-MAIN-015](UC-MAIN-015-sync-across-devices.md) — offline changes to a claimed group reach the
  server when the connection does.
- **Performance:** an offline edit is as fast as an account-free one; there is no waiting on a
  network call in the write path.

## Open questions

- **How visible should *not synced* be?** Recommendation: a status the user can find, not a badge on
  every screen. The failure mode to avoid is teaching people to ignore a permanent warning — the
  account-free app shows nothing and is trusted.
- **Should the user be told when last-write-wins discarded their version?** Recommendation: not in
  M3. Telemetry first; a message that says "your change was replaced" and offers nothing to do about
  it is worse than the collision. Revisit when the hook shows how often this happens.

## Traceability

- Requirements: [FR-024](../../requirements/functional/FR-024-offline-editing-and-reconciliation.md);
  constrained by
  [NFR-008](../../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md) and
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)
- ADRs: [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) (LWW + escalation hook),
  [ADR-0012](../../adr/0012-observability-and-telemetry.md),
  [ADR-0029](../../adr/0029-storage-write-failure-contract.md)
- Related use cases: [UC-MAIN-015](UC-MAIN-015-sync-across-devices.md),
  [UC-MAIN-005](UC-MAIN-005-manage-profile.md)
- Glossary: [Sync](../../glossary/sync.md), [Tombstone](../../glossary/tombstone.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  theme E
