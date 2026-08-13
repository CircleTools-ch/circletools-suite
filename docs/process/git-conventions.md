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

## Branch protection (deferred — 2026-08-13)
The repo (`CircleTools-ch/circletools-suite`) is **private on the GitHub Free plan**, where branch
protection is **not enforced on private org repos** — neither classic branch-protection rules nor
rulesets take effect until the org upgrades to **GitHub Team**, or the repo is made **public**. So
the PR flow above is currently **convention, not a server-side gate**: nothing hard-blocks a direct
push to `main`.

This is accepted for a solo maintainer — the only person who could bypass it is the maintainer, and
CI ([ADR-0038](../adr/0038-ci-cd-pipeline.md)) still **runs and reports** `build-test` / `docs` /
`build-maui` on every pull request, so the signal is intact even without the block. Keep working
through PRs by habit.

**Revisit when** either trigger fires:
- a **second contributor** joins — enforcement now guards *other people's* pushes, which is the real
  risk; upgrade to **GitHub Team** (~$4/mo, also lifts the 2,000 Actions-minutes/month cap that the
  Windows `build-maui` job spends at 2×), or
- the repo **goes public** — enforcement and Actions minutes both become free; then add a **ruleset**
  targeting the default branch requiring a PR and the three CI checks (see the ruleset config already
  worked out for this repo).

## Do not
- Don't commit secrets, real personal data, or build output.
- Don't bypass hooks/signing unless explicitly agreed.

## AI assistants & automation
- **Never perform git operations on your own.** Creating branches, staging, committing, pushing,
  merging, rebasing, or tagging happens **only** when the user explicitly orders that specific
  action, in that instance — prior approval does not carry over. Otherwise, leave changes in the
  working tree for the user to review and commit.
- **Keep all project knowledge in this repo** (Markdown under git). Do not rely on global/external
  memory or notes outside the repository. See the repository `CLAUDE.md` (golden rules 8–9).
