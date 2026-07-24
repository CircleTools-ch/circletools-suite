# 4. Solution Strategy

> _Proposed_ — this strategy is the recommended direction from harness design. It is ratified
> incrementally via [ADRs](09-architecture-decisions.md) during the architecture phase.

| Goal / constraint | Strategic decision |
|-------------------|--------------------|
| Local-first, offline-capable (Q1) | Clients always read/write a **local store (SQLite)** first; the domain is persistence-agnostic via **ports & adapters**. |
| Optional sync (Q1) | An account turns the local store into a **synced replica**; a sync engine reconciles with the server via delta APIs. |
| Cross-platform parity (Q2) | All feature UI lives in **Razor Class Libraries** consumed by both the Blazor Web host and the MAUI Blazor Hybrid host; platform gaps behind abstraction interfaces. |
| Maintainability & evolvability (Q4) | **DDD** with a **modular monolith** ([ADR-0003](../adr/0003-modular-monolith-with-aspire.md)); each app-type is a bounded context that can be extracted later. |
| Privacy (Q3) | Minimal personal data; no data leaves the device unless the user signs in; Swiss/GDPR-aware. |
| Localizability (Q5) | Central localization building block; `de-CH` default; DE/EN/FR resources. |
| Consistency & observability | **.NET Aspire** AppHost + `ServiceDefaults` (OpenTelemetry, health, resilience, discovery). |

## Key patterns

- **Clean/Hexagonal + DDD:** dependencies point inward; `Service.Core` defines repository
  **ports**; `Service.Repository` (Postgres) and `LocalStore` (SQLite) are **adapters** — the same
  use-case logic runs server-side and on-device.
- **CQRS-lite** in the application layer with pipeline behaviors (validation, logging, transactions,
  domain-event dispatch).
- **Contracts** per bounded context define the API surface; domain never depends on DTOs.

See the [building-block view](05-building-block-view.md) for the concrete structure.
