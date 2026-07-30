---
id: UC-MAIN-015
title: See my data on every device I sign in on
app: Main
actors: [User]
status: Approved
priority: Must
related: [FR-023, NFR-001, NFR-008, ADR-0016, ADR-0020]
---

# UC-MAIN-015 — See my data on every device I sign in on

## Summary / goal

A signed-in [User](../../glossary/user.md) works on their phone and their laptop and finds the same
data on both: the Secret-Santa [Groups](../../glossary/group.md) they took into their account, and
their profile preferences. [Sync](../../glossary/sync.md) is what makes the account worth having —
before it, an account only granted a name and a password.

## Primary actor & stakeholders

- **Primary actor:** a signed-in user on any of their devices.
- **Stakeholders & interests:** the organizer of a group, who must not have to redo work per device;
  the operator, who now holds a copy of that data and is answerable for it
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)); the account-free
  user, whose experience must be untouched
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Preconditions

- The actor is signed in ([UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md) or
  [UC-MAIN-004](UC-MAIN-004-sign-in-with-passkey.md)).
- At least one group has been taken into the account
  ([UC-SS-007](../secret-santa/UC-SS-007-claim-local-groups.md)), or the profile has been edited.

## Postconditions

- **Success:** every device the actor is signed in on holds the same set of account-scoped aggregates
  at the same versions; the local copy on each device remains usable with the network gone.
- **Failure:** nothing is lost. The device keeps its local state, shows that it is not in sync, and
  retries.

## Trigger

The app starts, the actor changes something, or connectivity returns.

## Main success scenario

1. The app starts with the actor signed in.
2. System **pulls** everything that changed on the server since the cursor it last recorded, and
   **pushes** every local aggregate whose version is ahead of the server's.
3. System applies the incoming aggregates to local storage as whole aggregates, and advances the
   cursor.
4. The actor's groups and preferences are the same here as on the device they last used.
5. The actor edits a group. System saves locally first, then pushes; the change reaches the other
   device on its next sync.

## Extensions / alternate flows

- **1a. First sign-in on a device with anonymous local data** → the claim runs first
  ([UC-SS-007](../secret-santa/UC-SS-007-claim-local-groups.md)); nothing is uploaded before the actor
  has chosen.
- **2a. A group was deleted on the other device** → a tombstone arrives and the group is removed here
  too. Deleting must propagate, or the group reappears on the next sync from the device that still
  has it.
- **2b. A group was removed from the account on the other device** (not deleted —
  [UC-SS-008](../secret-santa/UC-SS-008-remove-group-from-account.md)) → it stops syncing and the
  **local copy stays**. This is deliberately not the same signal as 2a: "take it out of my account"
  must never be a remote wipe of the other device.
- **3a. The same aggregate changed on both sides since the cursor** → last-write-wins by
  server-recorded time; the losing version is discarded and the collision is reported as telemetry
  ([UC-MAIN-016](UC-MAIN-016-work-offline-and-reconcile.md) covers this properly).
- **5a. The push fails** (offline, server error) → the edit stays local and queued; the device shows
  *not synced* and retries on the next trigger.
- **6. The actor signs out** → syncing stops. The local copy remains and the app keeps working
  account-free, exactly as it does for someone who never signed in.

## Exceptions

- **The access token expired mid-sync** → refreshed transparently
  ([ADR-0021](../../adr/0021-web-auth-token-model.md)); the sync resumes. A failed refresh signs the
  actor out and stops syncing — it does not touch local data.
- **The server rejects an aggregate** (schema too new, payload too large) → that aggregate is skipped
  and reported; the rest of the sync completes. One bad group must not block the others.
- **The device clock is wrong** → irrelevant to the outcome: ordering uses **server-recorded** time,
  never device time.
- **The account is deleted** ([UC-MAIN-006](UC-MAIN-006-delete-account-and-export-data.md)) → the
  server copies go with it; the local copies on every device survive as account-free data.

## Non-functional notes

- **Offline/local-first:** the device is authoritative and stays fully usable with no network. Sync is
  additive — it is a relay and a backup, never a gate in front of an edit
  ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)).
- **Localization:** sync status and its failures are ordinary UI strings in DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** this is the point at which a claimed group — **including its
  [Draw](../../glossary/draw.md) and therefore its [Assignments](../../glossary/assignment.md)** —
  first leaves the device. That is the amended
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md) and
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) taking effect, and the claim screen
  says so before it happens.
- **Performance:** a sync with nothing to do transfers no aggregate bodies; a family-scale account
  converges within one cycle of a few seconds
  ([NFR-008](../../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md)).

## Open questions

- **Does [Circle](../../glossary/circle.md) data sync too?** Recommendation, and the assumption the
  M3 requirements are written on: **no.** Circles stay server-authoritative and online-only as M2
  built them. Last-write-wins is defensible for an aggregate one account owns; on a circle that four
  people administer it would silently discard *other people's* changes, and every circle mutation is
  an authorization decision that has to be taken server-side
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)) — which is exactly
  what an offline write cannot do. This narrows
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md), which names the circle as a sync unit,
  so it needs an ADR before M3 is built rather than a quiet divergence in code.
- **Is a "sync now" button needed** on top of start / on-change / on-reconnect? Recommendation: yes,
  but as part of the sync-status surface rather than a feature of its own — when a push has failed,
  the user needs a way to try again that is not "close and reopen the app".

## Traceability

- Requirements: [FR-023](../../requirements/functional/FR-023-sync-across-devices.md); constrained by
  [NFR-008](../../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md),
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- ADRs: [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) (unit, cursor, tombstones, LWW),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md),
  [ADR-0008](../../adr/0008-entity-id-strategy.md) (client-minted ids, so an offline creation needs no
  server)
- Related use cases: [UC-MAIN-016](UC-MAIN-016-work-offline-and-reconcile.md),
  [UC-SS-007](../secret-santa/UC-SS-007-claim-local-groups.md),
  [UC-SS-008](../secret-santa/UC-SS-008-remove-group-from-account.md),
  [UC-MAIN-005](UC-MAIN-005-manage-profile.md) (the preference that could not be changed offline until
  now)
- Glossary: [Sync](../../glossary/sync.md), [Tombstone](../../glossary/tombstone.md),
  [Group](../../glossary/group.md), [User](../../glossary/user.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  theme E
