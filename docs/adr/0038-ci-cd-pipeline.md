---
status: Accepted
date: 2026-08-13
deciders: [Adrian]
tags: [ci-cd, build, testing, deployment, github, automation]
---

# ADR-0038 — CI/CD pipeline

## Context and problem statement

The suite has ~500+ tests across 14 test projects and a DocFX site that **fails the build on broken
links**, but there is **no continuous integration**: the code is green only on whoever last ran it
locally ([arc42 ch. 11](../arc42/11-risks-and-technical-debt.md) D5). The
[Definition of Done](../process/definition-of-done.md) requires "CI green" and
[git-conventions](../process/git-conventions.md) requires that pull requests "pass required checks
(build under analyzers, tests, docs build)" — neither can be satisfied while no automation runs those
checks. This is the last open item of **milestone 7** and a prerequisite for the PR-based workflow the
repo already prescribes.

The code now lives in the GitHub org **`CircleTools-ch`** (repo `circletools-suite`). Hosting is
decided — a single Infomaniak VPS running Docker Compose ([ADR-0036](0036-cloud-provider-and-hosting.md))
— and the iOS/macOS build host in [ADR-0031](0031-ios-build-host.md) settled the *build host* but
explicitly **not the pipeline**. So two questions remain: **what runs the build/test/docs gate, and
where does the eventual deploy step live?**

Two properties of this repository shape the answer:

- **The build is strict.** `Directory.Build.props` sets `TreatWarningsAsErrors`,
  `EnforceCodeStyleInBuild` and `RestorePackagesWithLockFile`; every project has a committed
  `packages.lock.json`. A warning is a failure, so the gate is meaningful without extra
  configuration. The lock files pin versions for local restores, but CI does **not** restore in
  strict `--locked-mode`: a few packages resolve **per-OS** (the RID-specific `Aspire.Dashboard.Sdk`)
  or **per-SDK-patch** (`Microsoft.NET.ILLink.Tasks`, pulled in for the Blazor WASM trimmer), so a
  lock file committed from one machine cannot satisfy a strict restore on a differently-provisioned
  runner. Enforcing locked-mode would make CI red for a package that changed only its RID or patch.
- **One project cannot build on a plain Linux runner.** The MAUI head
  `CircleTools.SecretSanta.App` needs the MAUI workload, and its target frameworks are scoped by OS
  (Linux → `net10.0-android` only; Windows adds the Windows head; macOS adds iOS/MacCatalyst). The
  library/web/API/test projects — everything the Definition-of-Done gate cares about — build on Linux
  with no workload.

## Decision drivers

- **Satisfy the Definition of Done and git-conventions** — build under analyzers, full test run, docs
  build, on every push and PR to `main`.
- **Cheap and low-maintenance** — one hobby maintainer; free-tier minutes must comfortably cover it.
- **Native to where the code lives** — the repo is on GitHub; avoid a second account/provider.
- **Reproducible** — pin the SDK (`global.json`) and keep the committed lock files as the version
  baseline (strict `--locked-mode` is not enforced in CI; see the context note above).
- **A path to CD and to iOS** — the same system must be able to deploy to the ADR-0036 VPS later and
  to host a macOS runner leg ([ADR-0031](0031-ios-build-host.md)) without being replaced.
- **Fast core gate** — the everyday build/test signal should not wait on MAUI workload provisioning.

## Considered options

1. **GitHub Actions** — hosted runners, workflows in `.github/workflows/`.
2. **Azure DevOps Pipelines** — the classic .NET CI, but a second identity/portal beside GitHub.
3. **Self-hosted runners only** — on the maintainer's hardware / the future Mac.
4. **No CI (status quo)** — keep verifying locally.

## Decision outcome

**Chosen: option 1 — GitHub Actions**, because the code already lives on GitHub, the free tier is
ample for this size, and it deploys to the ADR-0036 VPS over SSH and hosts a macOS runner leg
([ADR-0031](0031-ios-build-host.md)) when those arrive — one system from the core gate to CD.

### CI gate (built now)

A single workflow `.github/workflows/ci.yml`, triggered on `push` and `pull_request` to `main`,
pinning the SDK from `global.json` (restore uses the committed lock files but not strict
`--locked-mode` — see the context note above), with three parallel jobs:

- **`build-test`** (`ubuntu-latest`) — builds and tests the **non-MAUI** project set and carries the
  full test run. Because the MAUI head cannot build on a plain Linux runner, the set is a **second
  solution, `CircleTools.NoMaui.slnx`**, holding every project except `CircleTools.SecretSanta.App`.
  (A `.slnf` solution *filter* was rejected: filter files reference a `.sln`, and this repo is
  `.slnx`-only. The second solution must be kept in sync when projects are added — a note in the file
  says so, and an out-of-sync build fails loudly.)
- **`docs`** (`ubuntu-latest`) — `dotnet tool restore` then `dotnet docfx docfx/docfx.json`, which
  fails on broken links/xrefs ([ADR-0002](0002-use-arc42-c4-docfx.md)).
- **`build-maui`** (`windows-latest`) — `dotnet workload restore` then builds
  `CircleTools.SecretSanta.App` for its **Android and Windows** heads. **iOS is deferred** to a future
  macOS runner ([ADR-0031](0031-ios-build-host.md)); a plain Linux/Windows runner cannot produce it.

All three are intended as **required status checks** on `main`. They report independently, so a MAUI
provisioning hiccup does not hide the core build/test signal. If `build-maui` proves flaky or slow to
stabilise, it may be split into its own workflow so it never gates the core gate — a follow-up, not a
reason to hold this decision.

### CD (documented, deferred to milestone 8)

Continuous *deployment* is **not built now** — it needs the VPS to exist. The shape, per
[ADR-0036](0036-cloud-provider-and-hosting.md): a workflow builds container images, then **SSHes the
Infomaniak VPS and runs `compose pull && up`**, with **EF Core migrations as an explicit release
step**. Secrets (SSH key, DB credentials, the Brevo API key, signing assets) live in **GitHub Actions
secrets plus the host store, never in the repo** — this is ADR-0036's open "secrets management"
follow-up, to be recorded when CD lands. The macOS/iOS release leg rides
[ADR-0031](0031-ios-build-host.md)'s self-hosted Mac as a runner.

## Consequences

- **Positive:** the Definition of Done's "CI green" and git-conventions' "required checks" become
  enforceable; `main` can be protected so no PR merges red. The everyday gate is fast (Linux, no
  workload) while the MAUI heads still build each change.
- **Positive:** one system spans the core gate, the future VPS deploy, and the future macOS leg — no
  second CI provider to adopt later.
- **Negative / accepted:** `CircleTools.NoMaui.slnx` duplicates the project list; adding a project
  means updating both solutions. Cheap and self-announcing (the build breaks if forgotten), and it
  buys a fast workload-free core job.
- **Negative / accepted:** iOS is not built in CI until a macOS runner exists; until then iOS breakage
  is caught only on the Mac ([ADR-0031](0031-ios-build-host.md)).
- **Follow-ups:**
  - Enable **branch protection** on `main` requiring `build-test`, `docs`, `build-maui` (the checks
    appear in the protection UI only after the workflow has run once).
  - Build the **CD workflow** at [milestone 8](../roadmap/roadmap.md) against the provisioned VPS, and
    record **secrets management** (ADR-0036 follow-up) then.
  - Add the **macOS/iOS runner** leg once the Mac is acquired ([ADR-0031](0031-ios-build-host.md)).
  - Automate the **WebAuthn ceremony** and the invitation-link E2E (arc42 D4/D8) in the same
    browser-driving host — the remaining milestone-7 items, tracked separately from this ADR.
  - Decide the **production observability backend** (ADR-0036 / [ADR-0012](0012-observability-and-telemetry.md)
    follow-up) — unrelated to the CI gate but part of the deployment picture.
- **New risks:** CI runs execute repo workflow code with the runner's token; keep third-party actions
  pinned and secrets out of PR-triggered jobs from forks (not a concern for a single-maintainer repo
  today, worth stating before external contributors arrive).

## Pros and cons of the options

### 1. GitHub Actions (chosen)
- 👍 Native to the repo host; free minutes ample at this size; nothing else to sign up for.
- 👍 One system reaches the VPS (SSH deploy) and can host a self-hosted macOS runner later.
- 👍 First-class `setup-dotnet` + `global.json`, NuGet caching, `pull_request` gating.
- 👎 Vendor-specific YAML; hosted macOS minutes are pricey (mitigated by the self-hosted Mac).

### 2. Azure DevOps Pipelines
- 👍 Mature .NET CI; generous free tier.
- 👎 A second portal/identity beside GitHub for no benefit here; further from where the code lives.

### 3. Self-hosted runners only
- 👍 No hosted-minute cost; required anyway for the iOS leg.
- 👎 A single maintainer's machine as the only CI is a single point of failure and a maintenance
  chore; wrong default for the core gate. Adopted *additively* for the macOS leg, not as the base.

### 4. No CI (status quo)
- 👎 Cannot satisfy the Definition of Done or git-conventions; "green" means "green on one laptop".

## Links

- Related ADRs: [ADR-0031](0031-ios-build-host.md) (build host / future macOS runner),
  [ADR-0036](0036-cloud-provider-and-hosting.md) (CD target — VPS + Compose),
  [ADR-0002](0002-use-arc42-c4-docfx.md) (DocFX link-checked docs build),
  [ADR-0012](0012-observability-and-telemetry.md) (production observability, a deployment follow-up)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md),
  [11 Risks and technical debt](../arc42/11-risks-and-technical-debt.md) (D5)
- Process: [Definition of Done](../process/definition-of-done.md),
  [git-conventions](../process/git-conventions.md), [roadmap](../roadmap/roadmap.md) (milestone 7)
