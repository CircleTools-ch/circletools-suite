# Architecture Decision Records (ADRs)

Every significant architectural or technological decision is recorded here as an ADR
(MADR-style; see [ADR-0001](0001-record-architecture-decisions.md)). Copy
[`_template-adr.md`](_template-adr.md), number it sequentially (`NNNN-kebab-title.md`), and add
a row below. Implement only once an ADR is **Accepted**.

## Register

| # | Title | Status | Date |
|---|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-07-24 |
| [0002](0002-use-arc42-c4-docfx.md) | Document with arc42 + C4 + DocFX | Accepted | 2026-07-24 |
| [0003](0003-modular-monolith-with-aspire.md) | Start as a modular monolith orchestrated by Aspire | Accepted | 2026-07-24 |
| [0004](0004-plantuml-prerender.md) | Pre-render PlantUML diagrams to SVG | Accepted | 2026-07-24 |

## Open decisions backlog (to be written as we reach the architecture phase)

These are the genuinely open choices identified during harness design; they will be written up
as ADRs during the architecture phase (see [arc42 ch. 9](../arc42/09-architecture-decisions.md)):

1. Web render/hosting model (Blazor WASM-PWA vs Server vs Auto) given offline-first
2. Local↔server sync & conflict-resolution strategy
3. Entity ID strategy (client-generated GUID v7)
4. Anonymous→account data-migration ("claim") semantics
5. Web auth token model (BFF/cookie vs bearer) + passkey library
6. Application mediation (MediatR vs hand-rolled dispatcher)
7. Persistence approach (EF Core for Postgres + SQLite vs `sqlite-net`)
8. Localization resource format & culture routing (`de-CH` default)
9. Groups / sharing model across users
10. Suite packaging & app composition
11. Contracts / API style & versioning
12. Client telemetry & privacy/consent
13. Cloud provider & hosting target
