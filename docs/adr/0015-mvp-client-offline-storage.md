---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [secret-santa, persistence, offline, client]
---

# ADR-0015 — MVP client offline storage

## Context and problem statement

The Secret-Santa MVP is **offline and account-free** ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
[FEAT-001](../features/FEAT-001-secret-santa-mvp.md)): the organizer's data (groups, members,
wishlists, exclusions, draw history) must persist **on the client** with no backend. [ADR-0007](0007-persistence-approach.md)
chose EF Core for Postgres + SQLite but explicitly **deferred the web-client offline storage**
mechanism. The MVP ships two clients — a **Blazor WASM PWA** and a **MAUI Blazor Hybrid** app — so we
must decide how each persists locally now.

## Decision drivers

- No backend; fully offline; data volume is tiny (a few groups, each a handful of members).
- Keep the domain persistence-ignorant — storage sits behind a port so it is swappable.
- Simplest thing that is robust; minimal dependencies; fast startup.

## Considered options

1. **localStorage (WASM) + JSON file (MAUI)**, both behind one `IKeyValueStore` + `IGroupRepository`, sharing a `GroupState` JSON contract.
2. **IndexedDB** everywhere (via JS interop / a library).
3. **EF Core SQLite** everywhere (SQLite-in-WASM/OPFS on web).

## Decision outcome

**Chosen: option 1.** The domain defines `IGroupRepository`; a single `GroupRepository` serializes the
whole `GroupState` collection to JSON and stores it via an `IKeyValueStore`:

- **WASM:** `LocalStorageKeyValueStore` → the browser's `localStorage` via `IJSRuntime` (no custom JS).
- **MAUI:** `FileKeyValueStore` → one JSON file in `FileSystem.AppDataDirectory`.

`GroupState` (a memento produced by `Group.ToState()`/`FromState()`) keeps serialization out of the
domain. **EF Core SQLite is deferred** as a possible later native optimization (not needed at MVP
data sizes).

## Consequences

- **Positive:** trivially simple, no backend, offline; near-zero dependencies (`Microsoft.JSInterop`
  on web, `System.IO` on MAUI); one serialization contract for both clients; swappable behind the port.
- **Negative / accepted:** load-all/save-all (no partial queries) — fine for tiny data; `localStorage`
  ~5 MB soft limit (ample); **schema evolution is manual** — the persisted JSON carries a version and
  we migrate in code if `GroupState` changes.
- **Follow-ups:** if data grows or richer querying is needed, revisit with SQLite/IndexedDB (a future
  ADR). The v2 sync work builds on the same `IGroupRepository` port.

## Pros and cons of the options

### 1. localStorage + file (chosen)
- 👍 Simplest; offline; no real deps; one contract; port-swappable.
- 👎 Load-all only; manual schema versioning; localStorage size limit.

### 2. IndexedDB
- 👍 More capacity, async, structured.
- 👎 JS-interop layer / library; more complexity than the data warrants.

### 3. EF Core SQLite everywhere
- 👍 Matches ADR-0007's "EF everywhere"; querying + migrations.
- 👎 SQLite-in-WASM (OPFS) is experimental/heavy; slower startup; overkill for MVP.

## Links

- Related: [ADR-0007](0007-persistence-approach.md), [ADR-0005](0005-web-hosting-render-model.md), [ADR-0008](0008-entity-id-strategy.md)
- Feature: [FEAT-001](../features/FEAT-001-secret-santa-mvp.md); [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
