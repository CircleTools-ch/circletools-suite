---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, sync, offline, persistence, client]
---

# ADR-0016 — Local↔server sync & conflict resolution

## Context and problem statement

v2 gives the suite a [User](../glossary/user.md) account and cross-device
[Sync](../glossary/sync.md). Until now every client was an island: the Secret-Santa MVP keeps all
state in `localStorage` behind `IGroupRepository`
([ADR-0015](0015-mvp-client-offline-storage.md)) and never talks to a server
([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).

Once a signed-in user edits the same [Group](../glossary/group.md) on a phone and a laptop, two
copies of the truth exist. **What is the unit of synchronization, how do changes travel, how are
conflicts resolved, and what must the client now persist that it did not before?**

This ADR must also preserve the property that makes the product what it is: the app stays fully
usable with **no account and no network**. Sync is additive.

## Decision drivers

- **Local-first is non-negotiable.** The device stays authoritative and usable offline; the server is
  a relay and backup, never a gatekeeper (quality goal Q1, NFR-001).
- **Hobby scale.** A family has a handful of groups of a handful of people. Designing for
  Google-Docs-grade concurrency would be cost without benefit.
- **Genuine concurrent edits are rare.** Two organizers editing one group within the same sync window
  is an edge case, not the normal path.
- **The domain stays persistence-ignorant** — sync must sit behind the existing ports, not leak into
  aggregates.
- Aggregates are already the unit of persistence ([ADR-0007](0007-persistence-approach.md)) and IDs
  are already client-generated GUID v7 ([ADR-0008](0008-entity-id-strategy.md)) — offline creation
  needs no server round-trip to mint a key.

## Considered options

**Unit & conflict strategy**

1. **Aggregate as the sync unit, last-write-wins per aggregate**, with an escalation hook.
2. **Field-level merge** — reconcile property by property.
3. **CRDTs** — conflict-free replicated types for automatic convergence.
4. **Server-authoritative with pessimistic locking** — one writer at a time.

**Client storage under sync**

A. **Keep `localStorage` + the existing ports**, extending the persisted state with sync metadata.
B. **IndexedDB** via JS interop or a library.
C. **SQLite in WASM/OPFS** everywhere.

## Decision outcome

**Chosen: option 1 (aggregate unit + last-write-wins) and option A (keep `localStorage`).**

### Mechanism

- **The aggregate is the sync unit.** A Secret-Santa `Group` and a [Circle](../glossary/circle.md)
  each sync as a whole. Aggregates are never merged field-by-field — a group's members, exclusions
  and draw are a *consistent set*, and reconciling them independently can produce a state that no
  user ever created (e.g. a draw referencing a member removed on the other device).
- **Per-aggregate version token.** Every aggregate carries a monotonic version, bumped on each local
  change. The pair (aggregate id, version) is what pull and push compare.
- **Tombstones for deletes.** A delete writes a tombstone rather than removing the row, so the delete
  propagates. Without this, a delete on device A is indistinguishable from "not yet known to A" and
  the aggregate resurrects on the next pull.
- **Delta pull/push.** The client keeps a **cursor** (the server version it last saw) and exchanges
  only what changed on either side.
- **Triggers: on app start and on change** — not continuous background sync, which is parked
  (see below).
- **Last-write-wins per aggregate**, decided by server-recorded modification time, with an
  **escalation hook**: the reconciliation point is a named seam that detects a genuine
  divergent-edit collision and can raise it, so a conflict UI can be added later **without
  re-architecting**. Until such a UI exists the hook logs and applies LWW.

### Client storage

`localStorage` and the `IGroupRepository` / `IKeyValueStore` ports from ADR-0015 **stay**. The
persisted `GroupState` memento gains sync metadata (version token, tombstone flag, last-synced
cursor). ADR-0015's load-all/save-all trade-off is unchanged and still appropriate: the data is a few
kilobytes, and its ~5 MB budget remains ample.

This ADR therefore **extends ADR-0015 rather than superseding it**. IndexedDB or SQLite/OPFS is
revisited only on a concrete trigger — data volumes that make load-all/save-all slow, or a real need
for partial queries. Adopting either now would buy capacity we do not need at the cost of a JS-interop
layer or an experimental WASM SQLite build.

### What is explicitly parked

- **Continuous background sync** — start + on-change covers the actual usage pattern (someone opens
  the app, does something, closes it). Revisit if users report staleness.
- **A conflict-resolution UI** — ship LWW behind the escalation hook and find out whether collisions
  ever happen. Building conflict UX for a hypothetical is speculative work.

## Consequences

- **Positive:** offline behaviour is unchanged and NFR-001 holds in full; the domain is untouched
  (sync lives behind existing ports); tiny mechanism, few moving parts; client-generated GUID v7 keys
  mean offline-created aggregates need no server round-trip; the escalation hook keeps the upgrade
  path open.
- **Negative / accepted — LWW can lose an edit.** If two devices edit the *same* aggregate while both
  are offline, the later write wins and the earlier edit is **silently discarded**. This is a real
  data-loss window, accepted because the aggregate is small and the scenario (two organizers, same
  group, overlapping offline edits) is rare. The escalation hook exists precisely so this stops being
  silent when we learn it matters.
- **Negative / accepted:** aggregate-granularity means a one-word change to a group's name pushes the
  whole group. At this data size the wasted bytes are irrelevant.
- **Negative / accepted:** tombstones accumulate. They need a retention policy (prune after all known
  devices have advanced past them) — a detail for the sync implementation, not this ADR.
- **Negative / accepted:** clock-based LWW inherits clock-skew problems. Using **server-recorded**
  time rather than device time contains this, at the cost that a purely offline reorder is decided by
  arrival order at the server rather than by when the user actually typed it.
- **Follow-ups:** the wire format, endpoint shape and tombstone retention belong in the M3 feature
  spec; the claim of pre-existing local data is [ADR-0017](0017-anonymous-to-account-claim.md);
  authorization of synced assignment data is
  [ADR-0020](0020-server-side-assignment-privacy.md).
- **New risks:** the escalation hook is only useful if it is actually wired and observable — if it is
  a stub, we will not learn that collisions occur. It must emit telemetry
  ([ADR-0012](0012-observability-and-telemetry.md)).

## Pros and cons of the options

### 1. Aggregate unit + LWW (chosen)
- 👍 Simplest thing that converges; preserves aggregate consistency; trivial to reason about and test.
- 👎 Can silently discard a concurrent edit; coarse-grained transfers.

### 2. Field-level merge
- 👍 Fewer lost edits; both users' changes can survive.
- 👎 Breaks aggregate invariants — independently merged fields can yield a state no user created; far
  more machinery than a family-scale app justifies.

### 3. CRDTs
- 👍 Automatic convergence with no lost updates and no conflict UI ever.
- 👎 Substantial complexity and metadata overhead; hard to reconcile with DDD aggregates and EF Core;
  a research project relative to the problem.

### 4. Server-authoritative + locking
- 👍 No conflicts by construction.
- 👎 **Kills local-first** — editing would require connectivity, contradicting NFR-001 and quality
  goal Q1. Rejected outright.

### A. Keep localStorage (chosen)
- 👍 Zero new dependencies; ports already exist; ample for the data size; no migration.
- 👎 Load-all/save-all; manual schema versioning; ~5 MB ceiling.

### B. IndexedDB
- 👍 More capacity, async, structured, partial queries.
- 👎 A JS-interop layer or library for capacity we do not need.

### C. SQLite in WASM/OPFS
- 👍 Consistent with EF Core everywhere; real querying and migrations.
- 👎 Experimental and heavy in the browser; slower startup; overkill.

## Links

- Related ADRs: [ADR-0015](0015-mvp-client-offline-storage.md) (extended by this ADR),
  [ADR-0033](0033-which-aggregates-sync.md) (**narrows this ADR** — the Secret-Santa `Group` and
  profile preferences sync; the `Circle` does not),
  [ADR-0007](0007-persistence-approach.md), [ADR-0008](0008-entity-id-strategy.md),
  [ADR-0017](0017-anonymous-to-account-claim.md), [ADR-0020](0020-server-side-assignment-privacy.md),
  [ADR-0012](0012-observability-and-telemetry.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.2, §8.3
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md)
- Glossary: [Sync](../glossary/sync.md), [Group](../glossary/group.md), [Circle](../glossary/circle.md)
