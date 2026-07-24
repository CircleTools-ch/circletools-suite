# 9. Architecture Decisions

Decisions are recorded as [Architecture Decision Records](../adr/index.md) (MADR-style;
[ADR-0001](../adr/0001-record-architecture-decisions.md)).

## Accepted

| # | Decision |
|---|----------|
| [ADR-0001](../adr/0001-record-architecture-decisions.md) | Record architecture decisions |
| [ADR-0002](../adr/0002-use-arc42-c4-docfx.md) | Document with arc42 + C4 + DocFX |
| [ADR-0003](../adr/0003-modular-monolith-with-aspire.md) | Start as a modular monolith orchestrated by Aspire |
| [ADR-0004](../adr/0004-plantuml-prerender.md) | Pre-render PlantUML diagrams to SVG |
| [ADR-0005](../adr/0005-web-hosting-render-model.md) | Web hosting & render model (standalone WASM PWA) |
| [ADR-0006](../adr/0006-application-layer-and-mediation.md) | Application layer & mediation (hand-rolled dispatcher) |
| [ADR-0007](../adr/0007-persistence-approach.md) | Persistence approach (EF Core for Postgres + SQLite) |
| [ADR-0008](../adr/0008-entity-id-strategy.md) | Entity ID strategy (client-generated GUID v7) |
| [ADR-0009](../adr/0009-authentication-and-passkeys.md) | Authentication & passkeys (Identity + built-in passkeys) |
| [ADR-0010](../adr/0010-api-style-and-versioning.md) | API style & versioning (Minimal APIs) |
| [ADR-0011](../adr/0011-localization-format-and-culture.md) | Localization format & culture (RESX, `de-CH`) |
| [ADR-0012](../adr/0012-observability-and-telemetry.md) | Observability & client telemetry |

## Open (backlog)

The remaining decisions are **deferred on purpose** — they depend on the Secret-Santa domain model
(sync & conflict strategy, anonymous→account claim, groups/sharing model) or on deployment (suite
packaging, cloud provider). They are tracked with rationale and interim stances in the
[ADR register's open backlog](../adr/index.md#open-decisions-backlog).
