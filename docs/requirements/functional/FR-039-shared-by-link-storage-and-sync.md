---
id: FR-039
title: A shared-by-link group is stored on the server by share token and synced to link-holders
type: functional
status: Draft
priority: Must
source: UC-SS-017
verification: test
related: [UC-SS-017, UC-SS-019, FR-038, FR-040, FR-041, NFR-002, NFR-009, NFR-010, ADR-0058, ADR-0016, ADR-0033]
---

# FR-039 — A shared-by-link group is stored on the server by share token and synced to link-holders

## Description

In **Shared by link** mode, the whole [Group](../../glossary/group.md) aggregate — roster, exclusions,
budget, occasions, and each occasion's [Draw](../../glossary/draw.md) and
[Assignments](../../glossary/assignment.md) — is stored on the server, keyed by an unguessable
[share token](../../glossary/share-token.md) rather than by a [User](../../glossary/user.md) account
([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)). Holding a link that carries the token is
the credential (the *"the link is the secret"* model scaled to a whole group).

- **The aggregate is the unit of [sync](../../glossary/sync.md)** and is reconciled between the server
  and **every device holding an admin or join link** — not merged field-by-field
  ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0033](../../adr/0033-which-aggregates-sync.md)).
- **Conflict resolution is last-write-wins per aggregate**, exactly as for account sync; the difference
  is that the concurrent "writers" are several link-holders rather than one user's devices.
- **A device with a link works offline** and reconciles when connectivity returns, keeping the
  local-first behaviour.
- **Assignments now live on the server** for this mode, which is the amendment to
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md): secrecy becomes **trust-bounded**
  (see [FR-041](FR-041-join-link-and-person-claim.md) — a participant sees only the person they claimed).
- **The account-free write path is bounded** by anti-abuse and retention rules
  ([NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md),
  [NFR-010](../non-functional/NFR-010-shared-group-retention-lifecycle.md)).

## Rationale

The middle tier exists so co-organizers and participants can act on a group without an account, and to
survive across a user's devices without sign-in. Reusing the existing aggregate-sync machinery (the
sync unit, version tokens, tombstones, last-write-wins) keeps this a small, coherent extension rather
than a parallel stack — the only genuinely new element is *what authorizes* a pull/push (a share token,
not a user identity).

## Acceptance criteria

```gherkin
Given a group switched to "Shared by link" mode
When it is stored on the server
Then it is addressed by an unguessable share token and no user account owns it

Given two devices each holding a link to the same shared-by-link group
When one device edits the roster while offline and then reconnects
Then the change syncs to the other device, last-write-wins per aggregate

Given a shared-by-link group whose draw has run
When a link-holder opens the group
Then the aggregate (including the draw) is available from the server, subject to the person-claim privacy rules

Given a shared-by-link group
When the network is inspected during account-free use
Then the group is reachable only with a valid share token, never by guessing an id
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md),
  [UC-SS-019](../../use-cases/secret-santa/UC-SS-019-join-a-shared-group.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md) (mode selection),
  [FR-040](FR-040-admin-link-capability.md), [FR-041](FR-041-join-link-and-person-claim.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (amended — assignments on the server),
  [NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md),
  [NFR-010](../non-functional/NFR-010-shared-group-retention-lifecycle.md)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md); reuses
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) and
  [ADR-0033](../../adr/0033-which-aggregates-sync.md) (aggregate is the sync unit)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Share token](../../glossary/share-token.md), [Sync](../../glossary/sync.md),
  [Group](../../glossary/group.md), [Assignment](../../glossary/assignment.md)
