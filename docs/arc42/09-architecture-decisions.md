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
| [ADR-0013](../adr/0013-wasm-api-dev-wiring.md) | Standalone WASM ↔ API wiring (manual config + CORS) |
| [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md) | Secret-Santa reveal-link encoding & privacy |
| [ADR-0015](../adr/0015-mvp-client-offline-storage.md) | MVP client offline storage (localStorage / file) |
| [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) | Local↔server sync & conflict resolution (aggregate LWW) |
| [ADR-0017](../adr/0017-anonymous-to-account-claim.md) | Anonymous → account claim (choose per group) — superseded by ADR-0086 |
| [ADR-0018](../adr/0018-circle-and-sharing-model.md) | Circle & sharing model (canonical `Circle`, user-chosen label) |
| [ADR-0019](../adr/0019-member-user-linking.md) | Member↔User linking (optional `UserId`) — superseded by ADR-0086 |
| [ADR-0020](../adr/0020-server-side-assignment-privacy.md) | Server-side assignment storage & privacy (plaintext + authz) |
| [ADR-0021](../adr/0021-web-auth-token-model.md) | Web auth token model (bearer in WASM) |
| [ADR-0022](../adr/0022-account-deletion-and-data-export.md) | Account deletion & data export (immediate delete; export mirrors in-app visibility) |
| [ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md) | Local dev infrastructure in the AppHost (containers behind provider-agnostic seams) |

## Amended / partly superseded

| # | Change |
|---|--------|
| [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md) | **Partly superseded** by [ADR-0020](../adr/0020-server-side-assignment-privacy.md): assignments *are* stored server-side in account mode. The reveal-link decision itself stands and the account-free flow still depends on it, so it remains Accepted. |
| [ADR-0015](../adr/0015-mvp-client-offline-storage.md) | **Extended** by [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) with client-side sync metadata; `localStorage` stays. |
| [ADR-0009](../adr/0009-authentication-and-passkeys.md) | Its deferred web-token-model follow-up is **closed** by [ADR-0021](../adr/0021-web-auth-token-model.md). |

## Open (backlog)

The v2 domain decisions (sync, account claim, sharing model, Member↔User linking, assignment privacy,
web token model, deletion & export) are **now recorded** as ADR-0016…0022. What remains open depends
on the **deployment phase** — cloud provider & Swiss data residency, transactional email, CI/CD, suite packaging — or is
**parked behind a concrete trigger** (envelope encryption, IndexedDB, invite-by-link, conflict UI).
Tracked with rationale in the [ADR register's open backlog](../adr/index.md#open-decisions-backlog).
