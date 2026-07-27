# Roadmap

High-level sequence. Each milestone flows through the [ways of working](../process/ways-of-working.md).

```mermaid
flowchart LR
    H[0. Harness ✅] --> N[1. Naming & domain]
    N --> B[2. Brainstorm scope ✅]
    B --> R[3. Requirements / use cases ✅]
    R --> A[4. Architecture ADRs + arc42 ✅]
    A --> W[5. Walking skeleton - Aspire + Main shell ✅]
    W --> S[6a. Secret-Santa MVP ✅ - offline WASM+MAUI]
    S --> S2[6b. Accounts + sync + member links]
    S --> T[7. Testing]
    T --> D[8. Deployment - cloud + stores]
```

## Milestones

| # | Milestone | Outcome |
|---|-----------|---------|
| 0 | **Harness** | Repo, docs framework, process, DocFX site (this deliverable). ✅ |
| 1 | Naming & domain | Working name `AppSuite` in use across code + docs; **final brand deferred** (leading candidate: Walf, `walf.ch`); domain registration TBD. See [naming](../process/naming.md). |
| 2 | Brainstorm | ✅ AppSuite + Secret-Santa MVP scope decided: offline, account-free core with self-contained reveal links ([session](../brainstorming/2026-07-24-secret-santa-scope.md)). |
| 3 | Requirements | ✅ Secret-Santa MVP [use cases](../use-cases/index.md) (UC-SS-001..006) & [requirements](../requirements/index.md) (FR-001..006, NFR-001..003). |
| 4 | Architecture | ✅ ADR-0005..0021 accepted — MVP set (render model, mediation, persistence, IDs, auth/passkeys, API, localization, telemetry, WASM↔API, reveal-link privacy, client storage) plus the **v2 set**: [sync & conflicts](../adr/0016-sync-and-conflict-resolution.md), [account claim](../adr/0017-anonymous-to-account-claim.md), [circle model](../adr/0018-circle-and-sharing-model.md), [Member↔User linking](../adr/0019-member-user-linking.md), [server-side assignment privacy](../adr/0020-server-side-assignment-privacy.md), [web token model](../adr/0021-web-auth-token-model.md); arc42 updated. Only **deployment** choices remain deferred — see the [ADR backlog](../adr/index.md). |
| 5 | Walking skeleton | ✅ Aspire AppHost + ServiceDefaults + SharedKernel + Application + Main slice (API + WASM PWA) + Postgres, exercised by a throwaway `Heartbeat` sample; unit + ArchUnitNET tests; DocFX API reference generating. |
| 6 | Secret-Santa | **6a ✅** — local-first, offline MVP built (WASM PWA + MAUI Blazor Hybrid): group / members / wishlists / exclusions / fair draw (rules + history) / self-contained reveal links; localStorage (web) + file (MAUI); 22 Core tests. **6b (post-MVP)** — central login in the Main shell + App↔backend sync + linking Secret-Santa `Member`s to AppSuite user accounts. |
| 7 | Testing | Full test suite; CI green. |
| 8 | Deployment | Cloud (Web/API/DB under the suite domain) + app stores. |
