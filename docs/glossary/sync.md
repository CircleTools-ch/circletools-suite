---
term: Sync
contexts: [Platform]
status: Draft
---

# Sync

## Definition

Keeping what a device holds and what the server holds in step. There are two kinds ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)):

- **Online groups** have their own sync. An [organizer](organizer.md)'s device pushes its unsent edits, then
  reads the group back; a [participant](participant.md)'s device sends its own entry, then reads its own view. A
  signed-in device also discovers the account's groups and invitations. Groups kept **on this device** never
  reach the server ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **The account sync** carries the per-account documents — profile and UI preferences — between a signed-in
  user's devices.

Both are **local-first**: the device stays usable offline, and what it changed is sent when it is back online.

## Mechanism

- **Online groups:** one document per group on the server. A push carries the version it was based on; the
  rule is last write wins per group ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)). Members' own
  entries are stored apart and merged into the document on every write, so a push never overwrites them.
- **Account sync:** the document is the unit; each carries a version, deletes leave [tombstones](tombstone.md),
  a per-device cursor drives a delta exchange, and conflicts resolve last-write-wins by server time, counted
  through an escalation hook.
- **Not [Circles](circle.md).** Circle data is server-authoritative and online-only
  ([ADR-0033](../adr/0033-which-aggregates-sync.md)).

## Bounded context(s)

- **Platform:** the account sync is a cross-cutting capability; the store keeps each document opaque.
- **Secret-Santa:** the online-group sync is the app's own, against its group store.

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
  [Online group](online-group.md), [Place](place.md), [Tombstone](tombstone.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D4
