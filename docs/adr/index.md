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
| [0005](0005-web-hosting-render-model.md) | Web hosting & render model (standalone WASM PWA) | Accepted | 2026-07-24 |
| [0006](0006-application-layer-and-mediation.md) | Application layer & mediation (hand-rolled dispatcher) | Accepted | 2026-07-24 |
| [0007](0007-persistence-approach.md) | Persistence approach (EF Core for Postgres + SQLite) | Accepted | 2026-07-24 |
| [0008](0008-entity-id-strategy.md) | Entity ID strategy (client-generated GUID v7) | Accepted | 2026-07-24 |
| [0009](0009-authentication-and-passkeys.md) | Authentication & passkeys (Identity + built-in passkeys) | Accepted | 2026-07-24 |
| [0010](0010-api-style-and-versioning.md) | API style & versioning (Minimal APIs) | Accepted | 2026-07-24 |
| [0011](0011-localization-format-and-culture.md) | Localization format & culture (RESX, `de-CH`) | Accepted | 2026-07-24 |
| [0012](0012-observability-and-telemetry.md) | Observability & client telemetry | Accepted | 2026-07-24 |
| [0013](0013-wasm-api-dev-wiring.md) | Standalone WASM ↔ API wiring (manual config + CORS) | Accepted | 2026-07-24 |

## Open decisions backlog

The remaining open choices are **deferred on purpose** — each depends on the Secret-Santa domain
model (milestones 2–3) or on the deployment phase (milestone 8), and deciding them before then
would be premature. They will become ADRs when their inputs exist.

### Depends on the domain model (decide during MVP requirements)
- **Local↔server sync & conflict-resolution strategy.** *Interim stance:* local-first; aggregate =
  sync unit; per-aggregate version token + tombstones; delta pull/push; **last-write-wins per
  aggregate** as the starting default with an escalation hook. Includes the **web-client offline
  storage** sub-decision (WASM SQLite/OPFS vs IndexedDB).
- **Anonymous → account "claim"/migration semantics** (merge / replace / choose).
- **Groups / sharing model across users** (membership, cross-user sharing) — domain design.

### Partially settled, remainder deferred
- **Web auth token model** (bearer-in-WASM vs BFF/cookie). The identity/passkey stack is decided in
  [ADR-0009](0009-authentication-and-passkeys.md); the web token model is confirmed when the client shell is built.

### Depends on deployment
- **Suite packaging & app composition** (single shell hosting apps vs per-app installs).
- **Cloud provider & hosting target** (incl. Swiss data residency, CI/CD).
