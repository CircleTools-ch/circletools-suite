---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, persistence, ef-core, sqlite, postgres]
---

# ADR-0007 — Persistence approach

## Context and problem statement

The same domain must persist to **PostgreSQL on the server** and **SQLite on the device** (local-first).
`Service.Core` defines repository **ports**; we need to choose the persistence technology behind the
adapters (`Service.Repository` for Postgres, `LocalStore` for SQLite).

## Decision drivers

- One domain model, two stores — minimize duplicated mapping logic.
- Mature tooling (migrations, LINQ, provider support) on .NET 10.
- Keep the domain persistence-ignorant (no ORM attributes in entities).

## Considered options

1. **EF Core for both** Postgres (Npgsql) and SQLite
2. EF Core for Postgres + **`sqlite-net` (micro-ORM)** on device
3. Dapper / raw SQL for both

## Decision outcome

**Chosen: EF Core for both** PostgreSQL and SQLite. A single set of entity configurations (with
provider-specific tweaks where required) serves both; migrations are maintained **per bounded context**
and per provider. Mapping domain↔persistence stays in the infrastructure/adapter layer.

## Consequences

- **Positive:** One ORM, one mental model; shared configuration; LINQ + migrations on both sides;
  smooth path from local SQLite to server Postgres.
- **Negative:** EF Core on device adds size/startup cost vs a micro-ORM (acceptable for our data sizes);
  a few provider-specific differences (types, collation) to handle.
- **Follow-ups & open sub-decision:** The **web (WASM) client's** offline storage is *not* device SQLite;
  it will be either SQLite compiled to WASM (with OPFS persistence) or IndexedDB. This is decided together
  with the **sync strategy** (deferred) since it interacts with reconciliation.

## Pros and cons of the options

### 1. EF Core for both
- 👍 Uniformity, migrations, LINQ, provider maturity.
- 👎 Heavier on device than a micro-ORM.

### 2. EF Core + sqlite-net
- 👍 Tiny/fast on device.
- 👎 Two persistence models and mapping styles to maintain.

### 3. Dapper/raw SQL
- 👍 Full control, minimal overhead.
- 👎 Manual mapping/migrations; most work.

## Links

- arc42: [05 Building-block view](../arc42/05-building-block-view.md), [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Related: [ADR-0008](0008-entity-id-strategy.md); sync strategy (deferred — see [ADR register](index.md))
