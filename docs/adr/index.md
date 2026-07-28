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
| [0022](0022-account-deletion-and-data-export.md) | Account deletion & data export (immediate delete, export mirrors visibility) | Accepted | 2026-07-27 |
| [0023](0023-local-dev-infrastructure-in-apphost.md) | Local dev infrastructure in the AppHost (containers behind provider-agnostic seams) | Accepted | 2026-07-27 |
| [0024](0024-ui-styling-and-theming.md) | UI styling & theming (Bootstrap 5 + thin suite theme) | Accepted | 2026-07-28 |
| [0025](0025-shared-ui-building-blocks.md) | Shared UI building blocks (Ui.Abstractions, Localization, ApiClient, Ui.Shared) | Accepted | 2026-07-28 |
| [0026](0026-refresh-token-persistence.md) | Refresh-token persistence in the browser (session by default, durable on request) | Accepted | 2026-07-28 |
| [0027](0027-breached-password-check.md) | Breached-password check (bundled ≥12-character list) | Accepted | 2026-07-28 |
| [0028](0028-browser-platform-adapters-block.md) | Browser platform adapters as a shared block (`Ui.Web`) | Accepted | 2026-07-28 |
| [0029](0029-storage-write-failure-contract.md) | What an `IKeyValueStore` write does when storage refuses it | Accepted | 2026-07-28 |

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
- ✅ **Account deletion & data export** → [ADR-0022](0022-account-deletion-and-data-export.md)
  (immediate hard delete; the export reuses the ADR-0020 authorization boundary). Raised by the M1
  identity use cases, and legally due now that v2 stores PII. **Follow-up: NFR-004 (data protection &
  retention) does not exist yet** — first task of the M1 requirements step.

### Recently closed (M1 client — 2026-07-28)

Raised by building the Main app's Blazor client. The first two were being decided silently in code —
the template's Bootstrap, and a storage choice with no record; the third is a gap ADR-0021 left open
on purpose until there was a client to be concrete about.

- ✅ **UI styling & theming** → [ADR-0024](0024-ui-styling-and-theming.md) (Bootstrap 5 + a thin suite
  theme; brand deliberately absent, per [naming](../process/naming.md)).
- ✅ **Shared UI building blocks** → [ADR-0025](0025-shared-ui-building-blocks.md), making
  [arc42 ch.5](../arc42/05-building-block-view.md)'s planned blocks real. Its follow-up —
  Secret-Santa's own culture implementation — **is now closed** (see below).
- ✅ **Refresh-token persistence in the browser** →
  [ADR-0026](0026-refresh-token-persistence.md), extending
  [ADR-0021](0021-web-auth-token-model.md) with the session/durable split behind "stay signed in".

### Recently closed (Secret-Santa migration — 2026-07-28)

Raised by carrying out ADR-0025's recorded follow-up: migrating Secret-Santa onto the shared blocks.
Both decisions exist because the follow-up turned out to touch more than Secret-Santa — the second
web host made a Main-only adapter shared, and the second kind of caller made the storage contract
insufficient.

- ✅ **Where browser platform adapters live** → [ADR-0028](0028-browser-platform-adapters-block.md)
  (a new `AppSuite.Ui.Web` block), **amending** ADR-0025's "a host supplies the platform" rule.
  **Follow-up: the remaining three adapters** (`BrowserFileSaver`, `BrowserPasskeyCeremony`,
  `BrowserCredentialsHandler`) stay in `Main.WebApp` until a second host needs them.
- ✅ **Storage write-failure contract** → [ADR-0029](0029-storage-write-failure-contract.md)
  (`SetAsync` reports success), so a lost preference and a lost group stop being the same event.
  **Follow-up: the Secret-Santa UI does not yet present the failure** — the exception reaches
  Blazor's error UI, as a failed write already did.

### Depends on deployment (genuinely open)

> [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) lets **implementation** proceed without
> these, via containerised dev infrastructure behind provider-agnostic seams. It buys time; it does
> **not** close them, and M1 cannot ship until they are decided.

- **Cloud provider & hosting target**, including **Swiss data residency** — now due, because v2 stores
  personal data and assignments server-side. Also gates the passkey RP-ID
  ([FR-010](../requirements/functional/FR-010-manage-passkeys.md)) and the CSP directive set.
- **Transactional email provider** — required by **M1**, not only M2: verification
  ([FR-007](../requirements/functional/FR-007-create-suite-account.md)), password reset
  ([FR-009](../requirements/functional/FR-009-reset-password.md)) and email change
  ([FR-013](../requirements/functional/FR-013-change-email-address.md)) all depend on delivery.
  Invitations ([ADR-0018](0018-circle-and-sharing-model.md)) then add M2's onboarding dependency.
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
