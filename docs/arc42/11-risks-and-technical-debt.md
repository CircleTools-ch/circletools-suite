# 11. Risks and Technical Debt

> Living register. Add risks as they emerge; link mitigations to ADRs or backlog items.

## 11.1 Risks

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|-----------|------------|
| R1 | Sync/conflict complexity underestimated | High | Medium | Decide strategy early (sync ADR); start with simple last-write-wins per aggregate + escalation hook |
| R2 | Blazor render model choice hurts offline/first-load | Medium | Medium | Prototype WASM-PWA vs Auto before committing (render-model ADR) |
| R3 | Passkey/WebAuthn cross-platform effort | Medium | Medium | Spike per platform; isolate behind `AppSuite.Identity` |
| R4 | MAUI + strict analyzers/warnings-as-errors friction | Low | Medium | Scoped `NoWarn` for generated/MAUI code in `.editorconfig` |
| R5 | Scope creep (many apps too soon) | Medium | Medium | Roadmap discipline; ship Secret-Santa MVP first |

## 11.2 Technical debt

| # | Debt | Planned resolution |
|---|------|--------------------|
| D1 | API-reference section empty until code exists | ✅ Resolved 2026-07-28 — the `src/` projects exist and the reference generates from their XML doc comments |
| D2 | Guideline documents are outlines | Fleshed out as the relevant code is written |
| D3 | Placeholder brand name `Suite`/`<Suite>` | ✅ Resolved 2026-07-24 — finalized as **AppSuite**; token replaced repo-wide |
| D4 | **The WebAuthn ceremony is not automated.** Every passkey test is negative or an authorization check — none creates a credential, because an in-process `WebApplicationFactory` has no authenticator. The ceremony was verified by hand through a CDP virtual authenticator, but nothing re-checks it, and **NFR-004's "no private key, no biometric data" has no stored credential to inspect** | Milestone 7 — needs a browser-driving test host; carried from M1 ([TP-002](../features/TP-002-m1-identity.md) gap 4) |
| D5 | **No CI pipeline.** The suite is green only on whoever last ran it locally, so the Definition of Done's "CI green" cannot be met by any milestone | Milestone 7 — blocked on the CI/CD choice, an open item in the [ADR register](../adr/index.md#open-decisions-backlog); needs an ADR first |
| D6 | **M1 is built but cannot ship.** The transactional-email provider (FR-007/009/013 all depend on delivery) and the hosting/suite domain (passkey RP-ID, the CSP origin, the NFR-004 residency target) are both undecided | Deployment ADRs — the strategy for each is settled, only the provider and the literal domain are missing ([naming](../process/naming.md)) |
