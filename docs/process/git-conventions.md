# Git Conventions

## Branching
- `main` is always releasable.
- Work on short-lived branches off `main`:
  - `feat/<slug>` — new capability
  - `fix/<slug>` — bug fix
  - `docs/<slug>` — documentation only
  - `refactor/<slug>`, `test/<slug>`, `chore/<slug>`

## Commits — Conventional Commits
```
<type>(<optional scope>): <short summary>

<optional body — the why>
<optional footer — BREAKING CHANGE:, refs>
```
Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`.

Examples:
- `docs(harness): add arc42 chapters and ADR log`
- `feat(secret-santa): create group aggregate`

## Pull requests
- Small and focused; keep code and its docs in the same PR.
- PR description links the artifacts it satisfies (UC-…, FR-…, FEAT-…, ADR-…).
- Must satisfy the [Definition of Done](definition-of-done.md) and pass required checks
  (build under analyzers, tests, docs build).

## Reviews
- At least one review before merge (self-review acceptable while solo, but the checklist still applies).
- Prefer squash-merge for a clean, conventional history.

## Do not
- Don't commit secrets, real personal data, or build output.
- Don't bypass hooks/signing unless explicitly agreed.
