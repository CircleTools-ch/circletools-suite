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

Sync only ever applies to a signed-in user. Without an account there is no server, no sync, and the
app behaves exactly as it does today
([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).

## Mechanism

- The **aggregate is the unit of sync** — a Secret-Santa [Group](group.md), a [Circle](circle.md).
  Aggregates are never merged field-by-field.
- Each aggregate carries a **version token**; deletes leave **tombstones** so they propagate instead
  of resurrecting.
- Changes move as a **delta pull/push**, triggered on app start and on change (continuous background
  sync is parked).
- Conflicts resolve **last-write-wins per aggregate**, with an escalation hook for the rare genuine
  collision. A conflict-resolution UI is parked until collisions are observed in practice.

## Claim

The first sign-in on a device holding anonymous local data triggers a **claim**: the user is shown
their existing local groups and chooses **per group** which ones to take into the account. Claiming is
explicit, never automatic (decision D4).

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

- [User](user.md), [Circle](circle.md), [Group](group.md), [Assignment](assignment.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D4
