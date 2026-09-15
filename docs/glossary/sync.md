---
term: Sync
contexts: [Platform]
status: Draft
---

# Sync

## Definition

The reconciliation of data held locally on a device with the copy held on the server, so that a
signed-in [User](user.md) sees the same data on every device. Sync is **local-first**: the device
stays usable and authoritative offline, and the server is a relay and backup rather than a gatekeeper.

Sync is **opt-in and per-group**. A group stored **on the device** (the default) is not synced at all:
no server, no sync, exactly as the app behaves offline
([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)). A group reaches the server
in one of two ways — **synced to an account** (a signed-in user), or **shared by link**, an
account-free group the server holds keyed by an unguessable **share token** and reachable by an admin or
join link ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). The older "without an account
there is no server" wording predates that third mode and no longer holds in the absolute: what is
absolute is that **on-this-device groups never touch the server**.

## Mechanism

- The **aggregate is the unit of sync**. Aggregates are never merged field-by-field.
- Each aggregate carries a **version token**; deletes leave [tombstones](tombstone.md) so they
  propagate instead of resurrecting.
- The client keeps a **cursor** — the server state it last saw — and changes move as a **delta
  pull/push**, triggered on app start, on change, and when connectivity returns (continuous
  background sync is parked).
- Conflicts resolve **last-write-wins per aggregate**, decided by **server-recorded** time, with an
  escalation hook for the rare genuine collision. A conflict-resolution UI is parked until collisions
  are observed in practice — which is why the hook must emit telemetry rather than merely exist.

## What syncs

- **Secret-Santa [Groups](group.md)** that have been [claimed](claim.md) into an account — the whole
  aggregate, including the [Draw](draw.md) and its [Assignments](assignment.md).
- **Shared-by-link Groups** — the same aggregate, but keyed by a **share token** instead of a user id,
  reconciled between the server and every device holding an admin or join link
  ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). Conflict resolution is the same
  last-write-wins per aggregate; the "writers" are the link-holders rather than one user's devices.
- **Profile preferences** — display name and preferred language.
- **Not [Circles](circle.md).** Circle data is server-authoritative and online-only: last-write-wins
  would discard *other people's* edits on data several admins share, and an offline circle write
  cannot take the server-side authorization decision
  ([NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md)) that every circle
  mutation requires. This narrows [ADR-0016](../adr/0016-sync-and-conflict-resolution.md), which names
  the circle as a sync unit, and is recorded as
  [ADR-0033](../adr/0033-which-aggregates-sync.md). An offline circle *read* cache is a separate,
  parked decision — not offline circle *writes*, which stay excluded.

## Claim

The first sign-in on a device holding anonymous local data triggers a [**claim**](claim.md): the user
is shown their existing local groups and chooses **per group** which ones to take into the account.
Claiming is explicit, never automatic (decision D4), and the local copy is kept afterwards. Taking a
group back out of the account deletes the server copy only — it is not a delete, and it is not a
tombstone.

## Bounded context(s)

- **Platform:** a cross-cutting capability, not an aggregate of its own. Each app exposes its
  aggregates to it; the Secret-Santa client keeps its `IGroupRepository` port from
  [ADR-0015](../adr/0015-mvp-client-offline-storage.md), with a syncing implementation behind it.

## Synonyms / do-not-use

- **Synonyms:** synchronization.
- **Do NOT use:** "backup" (sync is bidirectional; backup is not); "replication" (implies
  server-to-server); "merge" for the whole process (merging is one possible conflict outcome);
  "cloud save".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Synchronisierung |
| EN | Sync |
| FR | Synchronisation |

## Related terms

- [User](user.md), [Circle](circle.md), [Group](group.md), [Assignment](assignment.md),
  [Claim](claim.md), [Tombstone](tombstone.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D4
