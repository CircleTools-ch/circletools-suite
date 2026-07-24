---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, persistence, ids, sync]
---

# ADR-0008 — Entity ID strategy

## Context and problem statement

Entities are created **offline on the client** and later synced to the server, so IDs must be
**generated on the client** without coordination and must not collide across devices. They also become
primary keys in both SQLite and PostgreSQL, so index locality matters.

## Decision drivers

- Offline creation with no server round-trip; no collisions across devices.
- Good database index behavior (avoid random-GUID page fragmentation).
- Native .NET support; stable across providers.

## Considered options

1. **Client-generated `Guid` v7** (`Guid.CreateVersion7()`, .NET 9+)
2. Client-generated `Guid` v4 (random)
3. Server-assigned sequential integers
4. ULID (via a library)

## Decision outcome

**Chosen: client-generated `Guid` version 7.** UUID v7 is **time-ordered**, so keys are largely
sequential (good clustered-index locality) while remaining globally unique and offline-generatable.
It is built into the BCL (`Guid.CreateVersion7()`), so no dependency is needed.

## Consequences

- **Positive:** Offline-safe, collision-free, index-friendly, no library; works identically for SQLite
  and Postgres; sortable-by-creation as a bonus.
- **Negative:** 16-byte keys (vs 4/8 for ints); v7 embeds a coarse timestamp (creation time is mildly
  inferable — acceptable, not secret).
- **Follow-ups:** Use `Guid` PKs across all aggregates; store as native `uuid` in Postgres and `TEXT`/`BLOB`
  in SQLite per EF Core provider conventions; document the rule in the [coding guidelines](../guidelines/coding-guidelines.md).

## Pros and cons of the options

### 1. Guid v7
- 👍 Offline, unique, time-ordered, BCL-native.
- 👎 16 bytes; timestamp inferable.

### 2. Guid v4
- 👍 Simple, unique.
- 👎 Random → index fragmentation.

### 3. Server sequential ints
- 👍 Small, fast joins.
- 👎 Requires server round-trip → breaks offline creation.

### 4. ULID
- 👍 Sortable, compact string form.
- 👎 Needs a library; v7 covers the same benefit natively.

## Links

- Related: [ADR-0007](0007-persistence-approach.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
