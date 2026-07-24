# 11. Risks and Technical Debt

> Living register. Add risks as they emerge; link mitigations to ADRs or backlog items.

## 11.1 Risks

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|-----------|------------|
| R1 | Sync/conflict complexity underestimated | High | Medium | Decide strategy early (sync ADR); start with simple last-write-wins per aggregate + escalation hook |
| R2 | Blazor render model choice hurts offline/first-load | Medium | Medium | Prototype WASM-PWA vs Auto before committing (render-model ADR) |
| R3 | Passkey/WebAuthn cross-platform effort | Medium | Medium | Spike per platform; isolate behind `Walf.Identity` |
| R4 | MAUI + strict analyzers/warnings-as-errors friction | Low | Medium | Scoped `NoWarn` for generated/MAUI code in `.editorconfig` |
| R5 | Scope creep (many apps too soon) | Medium | Medium | Roadmap discipline; ship Secret-Santa MVP first |

## 11.2 Technical debt

| # | Debt | Planned resolution |
|---|------|--------------------|
| D1 | API-reference section empty until code exists | Populates automatically once `src/` projects are added |
| D2 | Guideline documents are outlines | Fleshed out as the relevant code is written |
| D3 | Placeholder brand name `Suite`/`<Suite>` | ✅ Resolved 2026-07-24 — finalized as **Walf**; token replaced repo-wide |
