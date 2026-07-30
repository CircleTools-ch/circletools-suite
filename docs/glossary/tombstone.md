---
term: Tombstone
contexts: [Platform]
status: Draft
---

# Tombstone

## Definition

The record that an aggregate was **deleted**, kept in place of the aggregate itself so that the
deletion can travel. Without it, a delete on one device is indistinguishable from *not yet known to
that device*, and the next [sync](sync.md) resurrects what the user deleted.

A tombstone carries the aggregate's id and the version at which it was deleted, and nothing else — in
particular none of the deleted content.

Tombstones accumulate and need a retention policy: they may be pruned only once every device known to
the account has advanced past them. Pruning too early re-opens the resurrection the tombstone exists
to prevent.

## Bounded context(s)

- **Platform:** part of the sync mechanism, not of any app's domain model
  ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)). An aggregate's own invariants know
  nothing about it.

## Synonyms / do-not-use

- **Synonyms:** delete marker.
- **Do NOT use:** "soft delete" — a soft delete keeps the row and its data so it can be restored or
  reported on; a tombstone keeps only the fact of the deletion, and there is no restore. The
  distinction matters for [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md):
  a user-requested deletion must not leave the content behind.
- **Also not:** "archive", "trash", "recycle bin" — none of which this is.

## Product display terms (reference only — not code names)

None. A tombstone is never shown to a user; what they see is that the group is gone.

## Related terms

- [Sync](sync.md), [Claim](claim.md), [Group](group.md)

## First appearance

- [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) →
  [UC-MAIN-016](../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md)
