---
status: Accepted
date: 2026-08-21
deciders: [Adrian]
tags: [ci-cd, versioning, release, deployment, github, automation, maui, docker]
---

# ADR-0047 — Versioning & release strategy

## Context and problem statement

[ADR-0038](0038-ci-cd-pipeline.md) built the **CI gate** and left the **CD half** documented but
deferred to milestone 8, with two named follow-ups it did not resolve: *how versions are assigned* and
*what triggers a deploy*. Milestone 7 is now complete and the deploy scaffold exists
(`deploy/`, `.github/workflows/deploy.yml`, [ADR-0036](0036-cloud-provider-and-hosting.md) VPS +
Compose), so those follow-ups come due. Three questions must be answered together, because they
constrain each other:

1. **What advances the version, and how?** Today nothing does: assemblies carry no meaningful version,
   and the Secret-Santa MAUI head hard-codes `ApplicationDisplayVersion=1.0` / `ApplicationVersion=1`
   — a `versionCode` that can never change is unshippable to a store (Play requires a strictly
   increasing integer per upload). We want versions derived **automatically from git history**, not
   hand-edited.
2. **What triggers a deploy — a tag I set, or a merge to `main`?**
3. **Do all artifacts (web, API, Android, iOS) share one version?**

Two properties of this repo shape the answer. First, it is a **[federated suite of several apps in one
repo](0046-suite-packaging-and-app-composition.md)** whose shippable artifacts have **fundamentally
different release mechanics**: the web + API are containers we deploy to a VPS we own — instant,
reversible; the Android/iOS heads go through **store review** (hours–days) where the public version is
user-visible and the `versionCode` can never be reused or decreased. A single trigger cannot serve
both honestly. Second, the repository's Golden rules (`CLAUDE.md`, rule 9) require all knowledge to live
**in the repo under git** — so the version's source of truth should be a committed file, not an
out-of-band tag a maintainer must remember to place.

## Decision drivers

- **Automatic from git history** — no hand-edited version numbers; every merge to `main` produces a
  unique, increasing version with no ceremony.
- **Honest per-artifact mechanics** — server CD can be continuous; store releases must be deliberate
  and must feed a monotonic `versionCode`.
- **Provenance** — any container image or `.apk`/`.aab` must be traceable back to the exact commit.
- **Source of truth in the repo** (rule 9) — a committed file, not tags-as-only-state.
- **Cheap, low-maintenance, offline** — one hobby maintainer; no extra service, deterministic builds.
- **Path to per-app versions** — without a rewrite when Secret-Santa's cadence diverges from Main's.
- **Don't break the CI gate** or fail on a VPS that does not exist yet.

## Considered options

**Versioning mechanism**

1. **Nerdbank.GitVersioning (`nbgv`)** — committed `version.json` base + git *height*; SemVer + a
   monotonic build number; first-class MSBuild + GitHub Action.
2. **MinVer** — tag-driven; version only advances at a git tag (pre-release in between).
3. **GitVersion** — powerful, config-heavy, GitFlow-oriented.
4. **Hand-edited version** in `Directory.Build.props` — status quo, manual.

**Deploy trigger**

A. **Hybrid** — merge to `main` deploys the **server**; a `v*` tag cuts a **mobile** release.
B. **Tag-gated everything** — nothing deploys on merge; a tag releases server + mobile together.
C. **Push-gated everything** — merge to `main` deploys server *and* pushes mobile.

**Version scope**

α. **One suite version now, splittable later** — one `version.json`; nested ones added per app when
cadence diverges.
β. **Strictly one shared version** everywhere, always.
γ. **Independent per-app versions** from day one.

## Decision outcome

**Chosen: 1 + A + α — Nerdbank.GitVersioning, hybrid triggers, one suite version now with a split
path.**

### Versioning — Nerdbank.GitVersioning

A committed **`version.json`** at the repo root (base `"version": "0.1"`, pre-1.0) is the single source
of truth (rule 9). nbgv computes the version as `0.1.<git-height>` — the height being the number of
commits since the base version was set — so **every merge to `main` yields a unique, increasing
version automatically**, no tag required. `main` is declared a `publicReleaseRefSpec`, so its builds
drop the `+g<commit>` metadata and read as clean `0.1.<height>`; other branches keep the commit
suffix. The package is referenced **repo-wide** via `Directory.Build.props`, so nbgv auto-stamps
`AssemblyInformationalVersion` / `FileVersion` into **every** assembly — that is the provenance layer,
for free.

nbgv over the alternatives: MinVer leaves everything in `-alpha` between tags and hands us no
`versionCode`; GitVersion is disproportionate and historically finicky for a trunk-based
single-maintainer repo; hand-editing is exactly what we are removing. nbgv keeps the source of truth in
a git-committed file (rule 9), advances on every merge, and gives both a SemVer name and integers.

### Trigger — hybrid (and this is correct, not a compromise)

- **Web + API → merge to `main` deploys.** The server is ours; deploys are instant and reversible, so
  continuous deployment fits. `deploy.yml` runs after the CI gate **succeeds** (`workflow_run`), builds
  the API and Web images stamped with the nbgv version, and pushes them to GHCR — then rolls the VPS
  forward (SSH → migrator → `compose up`, per [ADR-0036](0036-cloud-provider-and-hosting.md)).
- **Android + iOS → a `v*` tag cuts a release.** Store review, an ever-increasing `versionCode`, and
  the no-rollback reality of stores make releases inherently deliberate. `release-mobile.yml` fires on
  `push: tags: ['v*']`, builds the signed head(s), and uploads to the store track. iOS rides the future
  macOS runner ([ADR-0031](0031-ios-build-host.md)).

So the answer to "tags or merge?" is **both, for different targets** — because the targets are not
alike. Tag-gating the server (B) would add manual ceremony to a deploy that wants to be continuous;
push-gating mobile (C) fights store review and `versionCode` rules.

### Scope — one suite version now, splittable later

One root `version.json` ⇒ one version stamped into every assembly and image for **provenance**: "which
commit is this?" is always answerable. But the **user-facing product version** of a store app (its
`versionName` / About screen) is a *separate concern* owned per app — it should not be inflated by how
many times we deployed the server. Today they coincide (one number everywhere); when Secret-Santa's
cadence diverges from Main's, drop a nested `version.json` into `src/Apps/<App>/` and nbgv scopes that
subtree independently — **no rewrite**. Starting unified therefore costs nothing and defers the
per-app split until it is earned. Strict single-version (β) would tie a store app's public version to
server deploys; full independence (γ) is moving parts we do not need yet.

### Android `versionCode` — git commit count, not git height

`versionCode` must be a **strictly increasing** integer that never repeats. nbgv's `VersionHeight`
**resets to 0 on every `major.minor` bump** in `version.json`, so it is *not* globally monotonic and is
unsafe as a raw `versionCode`. `release-mobile.yml` therefore sets `versionCode` from
`git rev-list --count HEAD` (monotonic across the whole history) and `versionName` from the nbgv SemVer,
passing both as MSBuild overrides to the signed build. The csproj keeps `1.0` / `1` only as local-dev
defaults.

### Container builds — version passed in, nbgv excluded

`.git` is excluded from the Docker build context (`.dockerignore`), so nbgv cannot run inside an image
build. `deploy.yml` computes the version once (the `dotnet/nbgv` action on a `fetch-depth: 0` checkout)
and passes it as `--build-arg VERSION=…`; the Dockerfiles publish with `/p:Version=$VERSION
/p:ContainerBuild=true`, and `Directory.Build.props` **omits the nbgv package reference when
`ContainerBuild=true`**. Deterministic, and it never depends on `.git` being in the image layer.

### Deploy stays auto-gated until the VPS exists

The VPS and `DEPLOY_*` secrets do not exist yet (milestone 8). So the **image build + push to GHCR runs
now** on every green merge, but the **SSH rollout job is gated on `DEPLOY_HOST` being set** (a preflight
job emits `deployable`). It activates itself the moment the secret is added — no code change, no failing
runs against a host that isn't there. This is ADR-0038's "flip to `on: push` once provisioning is done",
made automatic.

## Consequences

- **Positive:** versions are automatic, monotonic, and traceable to a commit, with the source of truth
  a git-committed file (rule 9). The Secret-Santa head becomes store-shippable (a real `versionCode`).
  Every green merge builds and publishes versioned images; the VPS rollout switches itself on at M8.
- **Positive:** one number today, a clean per-app split when earned — no rewrite.
- **Negative / accepted:** nbgv requires **full git history** in CI (`fetch-depth: 0`) — every checkout
  in `ci.yml`/`deploy.yml`/`release-mobile.yml` sets it; a shallow clone fails version-height
  calculation. Slightly larger clones, worth it.
- **Negative / accepted:** nbgv is referenced repo-wide, so a new package appears in every project's
  `packages.lock.json`. One-time churn; it is a `PrivateAssets=all` build-time dependency.
- **Negative / accepted:** the mobile release still needs signing + store-API secrets before it can
  *upload*; until then `release-mobile.yml` builds and archives the artifact (mirroring the deploy
  scaffold). Store credentials are the [ADR-0030](0030-mobile-store-publisher-identity.md) follow-up.
- **Follow-ups:**
  - At **launch**, bump `version.json` to `1.0` (one line).
  - Provision the VPS + `DEPLOY_*` secrets (milestone 8); the SSH job self-activates. Record **secrets
    management** (the open ADR-0036/0038 follow-up) then.
  - Wire the **store upload** legs (Play Developer API / App Store Connect) once signing secrets exist
    ([ADR-0030](0030-mobile-store-publisher-identity.md)); add the **macOS/iOS** leg with the Mac
    ([ADR-0031](0031-ios-build-host.md)).
  - Split `version.json` per app when Secret-Santa and Main diverge in cadence.
- **New risks:** `git rev-list --count HEAD` as `versionCode` assumes `main`'s history is **never
  rewritten** (it must not be, per [git-conventions](../process/git-conventions.md)); a rewrite could
  lower the count and block a store upload. Stated so it is a known invariant, not a surprise.

## Pros and cons of the options

### Versioning — 1. Nerdbank.GitVersioning (chosen)
- 👍 Source of truth is a committed `version.json` (rule 9); advances every merge with no tag.
- 👍 Gives a SemVer name *and* integers; auto-stamps every assembly; offline, deterministic, no server.
- 👎 Needs full git history in CI; `VersionHeight` resets on minor bumps (so we use commit-count for
  `versionCode`).

### Versioning — 2. MinVer
- 👍 Dead simple, tag-driven.
- 👎 Version only advances at a tag (pre-release in between); no `versionCode`; contradicts "automatic
  from history" for continuous server deploys.

### Versioning — 3. GitVersion
- 👍 Most configurable.
- 👎 GitFlow-oriented, config-heavy, historically finicky — disproportionate here.

### Trigger — A. Hybrid (chosen) / B. Tag-all / C. Push-all
- A 👍 matches each artifact's real mechanics; 👎 two workflows to reason about.
- B 👎 manual ceremony on a server deploy that wants to be continuous.
- C 👎 fights store review + `versionCode`; can't "continuously deploy" a store app.

### Scope — α. One-now-splittable (chosen) / β. Strict-single / γ. Per-app-now
- α 👍 provenance today, per-app when earned, no rewrite.
- β 👎 store app's public version inflated by server deploys.
- γ 👎 moving parts before they're needed.

## Links

- Related ADRs: [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD pipeline — this resolves its versioning +
  trigger follow-ups), [ADR-0036](0036-cloud-provider-and-hosting.md) (VPS + Compose CD target),
  [ADR-0031](0031-ios-build-host.md) (macOS/iOS runner), [ADR-0030](0030-mobile-store-publisher-identity.md)
  (store accounts / signing), [ADR-0046](0046-suite-packaging-and-app-composition.md) (federated suite:
  N apps, N store listings)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md)
- Process: [Definition of Done](../process/definition-of-done.md),
  [git-conventions](../process/git-conventions.md), [roadmap](../roadmap/roadmap.md) (milestone 8)
