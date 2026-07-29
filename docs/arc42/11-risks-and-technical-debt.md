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
| R6 | **iOS updates acquire an expiry date if no Apple-silicon Mac is bought.** .NET 10 needs Xcode 26, which needs macOS 26 — which only two Intel MacBook Pro models run at all, and macOS 27 (autumn 2026) drops Intel entirely. Apple's next spring SDK deadline then blocks every iOS update, including security fixes | High | High if deferred | Buy the build host ([ADR-0031](../adr/0031-ios-build-host.md)); until then treat any Intel Mac as a single-release bridge, not a plan |
| R7 | **Store account lock-out.** Publishing rights rest on one Apple ID and one Google account; losing 2FA access to either stops all updates for that platform, with no fast remedy | High | Low | Dedicated project accounts, password manager, **recovery codes stored offline** ([ADR-0030](../adr/0030-mobile-store-publisher-identity.md)) |
| R8 | **Permanent identifiers chosen before the brand is.** Bundle id / `ApplicationId` cannot change after the first release, and passkey RP-IDs do not migrate — a placeholder shipped once is shipped forever | High | Low | Brand + domain are a hard gate before first submission ([naming](../process/naming.md), [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)) |

## 11.2 Technical debt

| # | Debt | Planned resolution |
|---|------|--------------------|
| D1 | API-reference section empty until code exists | ✅ Resolved 2026-07-28 — the `src/` projects exist and the reference generates from their XML doc comments |
| D2 | Guideline documents are outlines | Fleshed out as the relevant code is written |
| D3 | Placeholder brand name `Suite`/`<Suite>` | ✅ Resolved 2026-07-24 — finalized as **AppSuite**; token replaced repo-wide |
| D4 | **The WebAuthn ceremony is not automated.** Every passkey test is negative or an authorization check — none creates a credential, because an in-process `WebApplicationFactory` has no authenticator. The ceremony was verified by hand through a CDP virtual authenticator, but nothing re-checks it, and **NFR-004's "no private key, no biometric data" has no stored credential to inspect** | Milestone 7 — needs a browser-driving test host; carried from M1 ([TP-002](../features/TP-002-m1-identity.md) gap 4) |
| D5 | **No CI pipeline.** The suite is green only on whoever last ran it locally, so the Definition of Done's "CI green" cannot be met by any milestone | Milestone 7 — blocked on the CI/CD choice, an open item in the [ADR register](../adr/index.md#open-decisions-backlog); needs an ADR first |
| D6 | **M1 is built but cannot ship.** The transactional-email provider (FR-007/009/013 all depend on delivery) and the hosting/suite domain (passkey RP-ID, the CSP origin, the NFR-004 residency target) are both undecided | Deployment ADRs — the strategy for each is settled, only the provider and the literal domain are missing ([naming](../process/naming.md)) |
| D7 | **The MAUI head still carries template identity.** `ApplicationId` is `com.companyname.appsuite.secretsanta.app`, the icon and splash are the .NET template art (`#512BD4`), and `RevealBaseUrl` points at `localhost:7081`. None of it is shippable | Blocked on the brand/domain decision, since the bundle id is permanent once released ([naming](../process/naming.md), [ADR-0030](../adr/0030-mobile-store-publisher-identity.md)) — then a small csproj + assets change |
