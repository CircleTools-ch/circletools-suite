---
status: Accepted
date: 2026-10-13
deciders: [Adrian]
tags: [documentation, ci-cd, deployment, security, docfx]
---

# ADR-0076 — Publish the documentation site to private GitHub Pages

## Context and problem statement

CI already **builds and link-checks** the DocFX site on every push and PR to `main`
([ADR-0002](0002-use-arc42-c4-docfx.md), the `docs` job in `ci.yml`), but it **throws the output away** —
there is no browsable, always-current site. The whole point of docs-as-code is a site people actually
read; today you must build it locally (`dotnet docfx … --serve`) to see it.

The repository is **private**, and the docs contain internal architecture and **security** detail: the
ADR register, [ADR-0020](0020-server-side-assignment-privacy.md) ("the operator can read every
giver→recipient pairing"), [ADR-0050](0050-secrets-management.md) (the secrets layout), and the new
[ADR-0075](0075-operator-read-only-database-access.md) (database access). Publishing that to a **public**
site would expose it to anyone with the URL — a real leak from a private repo. So the question is not just
*whether* to publish but *where*, such that the site is viewable by the people who should see it and no
one else.

**How should the DocFX site be published so it is a live, always-current internal wiki without exposing
the repository's internal docs?**

## Decision drivers

- **No public exposure of internal docs** — a private repo's security/architecture docs must stay
  behind authentication (security-privacy-guidelines; the same posture as [ADR-0020](0020-server-side-assignment-privacy.md)/
  [ADR-0050](0050-secrets-management.md)).
- **Always current** — deploys from `main` automatically, reusing the existing link-checked build.
- **Low operator overhead** — a solo maintainer; no extra infrastructure to run if avoidable.
- **Lives with the code** — GitHub-native, so the "wiki" is next to the repo it documents.
- **Degrades safely** — if the hosting prerequisite is not met, the result must be *no site*, never an
  accidentally public one.

## Considered options

1. **Private GitHub Pages** — deploy the site to GitHub Pages with **access control** so only repo/org
   members (authenticated) can view it. Requires a GitHub plan that supports Pages visibility = private
   (Team/Enterprise).
2. **Public GitHub Pages** — the simplest browsable site, but world-readable.
3. **Build artifact only** — keep building + link-checking in CI and upload the site as a downloadable
   workflow artifact; no live site.
4. **Self-host on the VPS** — serve `docs.circletools.ch` from the Infomaniak box behind Caddy basic-auth,
   alongside the app stack.

## Decision outcome

**Chosen: option 1 — private GitHub Pages**, with option 3 as the automatic fallback. A `docs-pages.yml`
workflow builds the DocFX site on every push to `main` (the same `dotnet docfx docfx/docfx.json` the CI
`docs` job runs) and uploads it as the Pages artifact; a **gated** deploy job publishes it to GitHub
Pages. Access control is set to **private** in the repository's Pages settings, so the site requires a
GitHub login with repo access.

Crucially, the deploy job is **gated on a repository variable** (`DOCS_PAGES_ENABLED == 'true'`), mirroring
how `deploy.yml` self-activates only once `DEPLOY_HOST` exists. Until the owner has enabled Pages with
private visibility **and** set that variable, the build/upload runs (keeping the artifact available and
the link-check honest) but **nothing is published** — so the default state is *no site*, never a public
one. This satisfies "degrades safely": enabling public Pages is an explicit, deliberate action that this
ADR does not take.

Option 2 is rejected outright — it would publish internal security docs to the open web. Option 3 is kept
as the fallback (and is what the workflow does with the deploy gated off) but is not the goal: an artifact
you download and open locally is not the live wiki asked for. Option 4 is viable and matches the Swiss
self-hosted posture, but adds a service to run and secure on the VPS for a benefit (a docs site) that
GitHub hosts for free behind the same account that already gates the private repo; revisit it only if the
GitHub plan cannot do private Pages, in which case self-hosting beats making the site public.

## Consequences

- **Positive:** an always-current internal wiki at the org's Pages URL, authenticated to repo members;
  reuses the existing link-checked build; no new infrastructure; GitHub-native.
- **Negative / accepted:** depends on a GitHub plan that supports **private** Pages (Team/Enterprise) — on
  a plan without it, Pages would be public, so the gate stays off and the site falls back to the CI
  artifact (option 3) until either the plan is upgraded or option 4 is adopted. One-time manual owner
  setup (enable Pages, set visibility, set the variable). Project-Pages subpath (`/<repo>/`) may need a
  base-URL tweak if asset links break (DocFX emits relative links, so this is usually fine).
- **Follow-ups:**
  1. Owner one-time enablement — see [docs/deployment/docs-site.md](../deployment/docs-site.md).
  2. If the plan lacks private Pages, either upgrade or implement option 4 (Caddy + auth on the VPS); do
     **not** flip the gate on with public visibility.
- **New risks:** a misconfiguration that sets Pages to *public* would expose internal docs — mitigated by
  the gate defaulting off and the runbook calling out the visibility setting as the load-bearing step.

## Pros and cons of the options

### 1. Private GitHub Pages (chosen)
- 👍 Live, always-current, authenticated to repo members; GitHub-native; reuses the CI build; no infra.
- 👎 Needs a Team/Enterprise plan for private visibility; one-time manual enablement; subpath base-URL caveat.

### 2. Public GitHub Pages
- 👍 Simplest; works on any plan.
- 👎 Publishes internal security/architecture docs to the open web — rejected.

### 3. Build artifact only
- 👍 Zero exposure; works on any plan; already most of the way there.
- 👎 Not a live site — you download and open it; no browsable wiki. Kept as the gated-off fallback.

### 4. Self-host on the VPS (Caddy + auth)
- 👍 Matches the Swiss self-hosted posture; full control of auth.
- 👎 A service to run, secure and TLS on the box for what GitHub hosts free behind the same account; more
  overhead than option 1. The fallback if private Pages is unavailable.

## Links

- Related ADRs: [ADR-0002](0002-use-arc42-c4-docfx.md) (docs with DocFX — this gives the site a home),
  [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD pipeline — this adds the docs-deploy leg),
  [ADR-0036](0036-cloud-provider-and-hosting.md) (hosting posture — why self-hosting is the plan-B, not
  plan-A, for docs), [ADR-0020](0020-server-side-assignment-privacy.md) /
  [ADR-0050](0050-secrets-management.md) (why the docs must not be public)
- Runbook: [docs/deployment/docs-site.md](../deployment/docs-site.md)
- Guideline: [documentation-guidelines.md](../guidelines/documentation-guidelines.md)
