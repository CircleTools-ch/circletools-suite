---
status: Accepted
date: 2026-11-13
deciders: [Adrian]
tags: [open-source, security, privacy, ci, documentation]
---

# ADR-0093 — Publish the repository on GitHub (history clean-up, public docs, safe CI)

## Context and problem statement

With a license chosen ([ADR-0099](0099-apache-2-license.md)), the repository is to
become public. Making a repository public cannot be undone in practice: clones, forks and caches keep
whatever was visible. Today the repo was written for a private audience:

- the **whole git history** becomes readable, including commit author emails and every file ever
  committed;
- docs contain operational detail (server setup, operator database access, alerting, ntfy, device-test
  notes that may name real people);
- [ADR-0076](0076-docs-site-to-private-github-pages.md) keeps the docs site private precisely because of
  that detail;
- CI workflows hold signing and deploy secrets, and would start running for pull requests from
  strangers.

**What must happen before the switch to public, and how is the repository run afterwards?**

## Decision drivers

- No secret and no real personal data may become public ([security & privacy
  guidelines](../guidelines/security-privacy-guidelines.md); "Don't commit secrets or real personal data").
- Security must not depend on the code being hidden: anything that is only safe while private must be
  fixed, not hidden.
- Keep as much of the useful history (ADRs, traceability, blame) as is safe.
- A pull request from a fork must never reach a secret or a release runner.

## Considered options

1. **Publish the existing repository after an audit, with history rewritten only where needed**
2. **Start a fresh public repository with a single squashed initial commit**
3. **Publish code only, keep docs in a separate private repository**

## Decision outcome

> **Accepted 2026-11-17 (owner).** Checklist items 1–3 had already been carried out on 2026-11-14, before
> acceptance: two scanners plus a pattern scan found no secrets; the history was rewritten (one screenshot
> removed, author email moved to the GitHub `noreply` address, two addresses and the server IP replaced) and
> force-pushed. The owner decided **not** to have GitHub purge the old commits that the 30 closed pull requests
> still reference, so the old author address and that screenshot stay reachable there once the repository is
> public. Details and the remaining flip-day steps: backlog OSS-2…OSS-5.

**Chosen: Option 1 — audit and publish the existing repository**, falling back to Option 2 only if the
audit finds something in history that cannot be removed cleanly. Option 3 is rejected: docs-as-code is
a golden rule, and most of the docs (ADRs, specs, guidelines) are exactly what contributors need.

**Before the switch (a checklist, each item ticked in the backlog):**

1. **Secret scan of the full history** with `gitleaks` and `trufflehog`; any hit is rotated first, then
   removed from history (`git filter-repo`). Rotation is mandatory even after removal.
2. **Personal-data review of the full history:** commit author emails (switch future commits to the
   GitHub `noreply` address; decide whether to rewrite past ones with a mailmap), real names in
   device-test and brainstorming notes, screenshots with real data, IP addresses and host names.
3. **Operational docs review:** anything that would help an attacker *only because it is specific to
   our server* (exact hostnames of admin tools, user names, internal paths of the backup target) moves
   into the password manager or the operator's private notes ([ADR-0050](0050-secrets-management.md)).
   Architecture and security *design* stays public — it is meant to be reviewed.
4. **CI hardening:**
   - release and deploy secrets live in GitHub **Environments** (`production`, `store-release`) with the
     owner as required reviewer, so a workflow change in a PR cannot read them;
   - no `pull_request_target` workflow checks out PR code;
   - "Require approval for all outside collaborators" for workflow runs;
   - third-party actions pinned to a commit SHA.
5. **Repository settings:** branch protection on `main` (PR + green CI), private vulnerability reporting
   on, Dependabot alerts on, a `SECURITY.md`, issue and PR templates, and CODEOWNERS.

**After the switch:**

- The docs site is published to **public** GitHub Pages; ADR-0076 is superseded by this ADR.
- The GHCR images become public packages (see [ADR-0098](0098-container-image-distribution.md)).

## Consequences

- **Positive:** history and decisions stay intact for contributors; the switch is a checklist, not a
  judgement call; CI is safe for outside PRs.
- **Negative:** a few hours of audit work; a history rewrite (if needed) changes every commit hash, so all
  clones and open worktrees must be re-cloned.
- **Follow-ups:** the checklist above (backlog OSS-2…OSS-5); supersede ADR-0076 once public.
- **New risks:** something missed by the audit becomes public for good — hence two scanners plus a manual
  review, and rotation of anything that was ever committed, even briefly.

## Pros and cons of the options

### Audit and publish existing repo
- 👍 Keeps history, blame and the ADR trail.
- 👎 Audit effort; history rewrite risk.

### Fresh squashed repository
- 👍 Nothing old can leak.
- 👎 Loses history and the traceability of when and why things changed; two repos to keep apart.

### Code public, docs private
- 👍 Operational docs stay hidden.
- 👎 Breaks docs-as-code and leaves contributors without the decisions behind the code.

## Links

- Related ADRs: [ADR-0076](0076-docs-site-to-private-github-pages.md) (to be superseded),
  [ADR-0050](0050-secrets-management.md) (secrets), [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD),
  [ADR-0099](0099-apache-2-license.md) (license)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (OSS-2…OSS-5)
