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
| [0014](0014-reveal-link-encoding-and-privacy.md) | Secret-Santa reveal-link encoding & privacy | Accepted | 2026-07-24 |
| [0015](0015-mvp-client-offline-storage.md) | MVP client offline storage (localStorage / file) | Accepted | 2026-07-24 |
| [0016](0016-sync-and-conflict-resolution.md) | Local↔server sync & conflict resolution (aggregate LWW) | Accepted | 2026-07-27 |
| [0017](0017-anonymous-to-account-claim.md) | Anonymous → account claim (choose per group) | Accepted | 2026-07-27 |
| [0018](0018-circle-and-sharing-model.md) | Circle & sharing model (canonical `Circle`, user-chosen label) | Accepted | 2026-07-27 |
| [0019](0019-member-user-linking.md) | Member↔User linking (optional `UserId`) | Accepted | 2026-07-27 |
| [0020](0020-server-side-assignment-privacy.md) | Server-side assignment storage & privacy (plaintext + authz) | Accepted | 2026-07-27 |
| [0021](0021-web-auth-token-model.md) | Web auth token model (bearer in WASM) | Accepted | 2026-07-27 |

## Open decisions backlog

The Secret-Santa MVP shipped, and the **v2 domain decisions that were deferred behind it are now
recorded** as ADR-0016…0021, resolved by the
[accounts, families & sync brainstorming session](../brainstorming/2026-07-24-accounts-families-sync.md).
What remains open genuinely depends on the deployment phase or on evidence we do not yet have.

### Recently closed (v2 — 2026-07-27)
- ✅ **Sync & conflict resolution** → [ADR-0016](0016-sync-and-conflict-resolution.md). Includes the
  web-client offline storage sub-decision: `localStorage` per
  [ADR-0015](0015-mvp-client-offline-storage.md) **stays**, extended with sync metadata.
- ✅ **Anonymous → account claim** → [ADR-0017](0017-anonymous-to-account-claim.md) (choose per group).
- ✅ **Circle & sharing model across users** → [ADR-0018](0018-circle-and-sharing-model.md).
- ✅ **Member↔User linking** → [ADR-0019](0019-member-user-linking.md) (optional `UserId`).
- ✅ **Server-side assignment storage & privacy** → [ADR-0020](0020-server-side-assignment-privacy.md);
  **partly supersedes** [ADR-0014](0014-reveal-link-encoding-and-privacy.md) and amends
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md).
- ✅ **Web auth token model** → [ADR-0021](0021-web-auth-token-model.md) (bearer in WASM), closing the
  follow-up deferred in [ADR-0009](0009-authentication-and-passkeys.md).

### Depends on deployment (genuinely open)
- **Cloud provider & hosting target**, including **Swiss data residency** — now due, because v2 stores
  personal data and assignments server-side.
- **Transactional email provider** — invitations ([ADR-0018](0018-circle-and-sharing-model.md)) make
  email delivery a hard onboarding dependency.
- **CI/CD pipeline.**
- **AppSuite packaging & app composition** (single shell hosting apps vs per-app installs).

### Parked — revisit on a concrete trigger
- **Per-member envelope encryption of assignments** (option P3 in
  [ADR-0020](0020-server-side-assignment-privacy.md)) — if operator-as-trusted-party stops being
  acceptable.
- **IndexedDB / SQLite-in-OPFS client storage** — if data volume or partial-query needs outgrow
  load-all/save-all ([ADR-0016](0016-sync-and-conflict-resolution.md)).
- **Invite by link or short code** — if email delivery proves to be the friction point.
- **Conflict-resolution UI** and **continuous background sync** — if the escalation hook in ADR-0016
  shows collisions actually occur.
