# 4. Solution Strategy

> Key strategic decisions are now **ratified** via [ADR-0005–0012](09-architecture-decisions.md).
> Items that depend on the domain model or deployment remain deferred (see the
> [ADR register](../adr/index.md)).

| Goal / constraint | Strategic decision |
|-------------------|--------------------|
| Local-first, offline-capable (Q1) | Clients always read/write a **local store (SQLite)** first; the domain is persistence-agnostic via **ports & adapters**. |
| Optional sync (Q1) | An account turns the local store into a **synced replica**; a sync engine reconciles with the server via delta APIs. |
| Cross-platform parity (Q2) | All feature UI lives in **Razor Class Libraries** consumed by both the **standalone Blazor WebAssembly PWA** and the MAUI Blazor Hybrid host; platform gaps behind abstraction interfaces ([ADR-0005](../adr/0005-web-hosting-render-model.md)). |
| Maintainability & evolvability (Q4) | **DDD** with a **modular monolith** ([ADR-0003](../adr/0003-modular-monolith-with-aspire.md)); each app-type is a bounded context that can be extracted later. |
| Privacy (Q3) | Minimal personal data; no data leaves the device unless the user signs in; Swiss/GDPR-aware. |
| Localizability (Q5) | Central localization building block; `de-CH` default; DE/EN/FR resources. |
| Consistency & observability | **.NET Aspire** AppHost + `ServiceDefaults` (OpenTelemetry, health, resilience, discovery). |

## Key patterns

- **Clean/Hexagonal + DDD:** dependencies point inward; `Service.Core` defines repository
  **ports**; `Service.Repository` (Postgres) and `LocalStore` (SQLite) are **adapters** — the same
  use-case logic runs server-side and on-device.
- **CQRS-lite** via a **hand-rolled dispatcher** in the application layer with pipeline behaviors
  (validation, logging, transactions, domain-event dispatch) — [ADR-0006](../adr/0006-application-layer-and-mediation.md).
- **Persistence:** EF Core for both Postgres (server) and SQLite (device); client-generated **GUID v7**
  keys — [ADR-0007](../adr/0007-persistence-approach.md), [ADR-0008](../adr/0008-entity-id-strategy.md).
- **Contracts** per bounded context define the API surface (Minimal APIs, versioned —
  [ADR-0010](../adr/0010-api-style-and-versioning.md)); domain never depends on DTOs.

See the [building-block view](05-building-block-view.md) for the concrete structure.
