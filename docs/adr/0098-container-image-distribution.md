---
status: Proposed
date: 2026-11-13
deciders: [Adrian]
tags: [deployment, docker, release, supply-chain, self-hosting]
---

# ADR-0098 — Public container images: multi-arch, release channels, signed

## Context and problem statement

`deploy.yml` already builds the API and web images and pushes them to
GHCR (`ghcr.io/circletools-ch/circletools-api`, `…-web`) tagged `:<version>`, `:<commit-sha>` and
`:latest` ([ADR-0047](0047-versioning-and-release-strategy.md)). The packages are private, built for
`amd64` only, and **every manual deploy** produces `:latest` — fine for our single server, not for
strangers who pull whatever `:latest` is.

**How are images published so that self-hosters can install, update and verify them safely?**

## Decision drivers

- Self-hosters run on Raspberry Pis and NAS boxes (`arm64`) as often as on `amd64`.
- An update must be a deliberate, documented step: a self-hoster should not get an untested build.
- Users must be able to verify that an image was built by our CI from this repository.
- Keep one build pipeline for our service and for self-hosters.

## Considered options

1. **GHCR, public, multi-arch, release channels (`stable`, `edge`), signed with provenance**
2. **Docker Hub in addition to GHCR**
3. **Keep the current tags, just make the packages public**

## Decision outcome

**Chosen: Option 1.** Docker Hub (Option 2) can be added later as a mirror if users ask for it.

- **Registry:** GHCR, packages **public**, linked to the repository.
- **Architectures:** `linux/amd64` and `linux/arm64` via buildx. The .NET build stays cross-compiled
  (`--platform=$BUILDPLATFORM` for the SDK stage) so `arm64` does not run under emulation.
- **Tags:**
  - `:<version>` and `:<commit-sha>` for every build (as today);
  - `:edge` — the last build from `main` (what our server runs, replaces today's `:latest`);
  - `:stable` and `:<major>`, `:<major>.<minor>` — only for a **release** (a git tag / GitHub Release
    with release notes);
  - `:latest` is an alias of `:stable`, so a naive pull gets a release, not a work-in-progress build.
- **Releases:** a GitHub Release per server release, with notes covering: migrations, configuration
  changes, breaking changes, and the minimum app version it supports and the minimum server version the
  current apps need ([ADR-0096](0096-client-server-selection-and-discovery.md)).
- **Supply chain:** images signed with **cosign** (keyless, GitHub OIDC), with **SLSA provenance** and an
  **SBOM** attached by `docker/build-push-action`. The self-hosting guide shows the `cosign verify`
  command.
- **Base images** pinned by digest and refreshed by Dependabot; images run as a non-root user.

## Consequences

- **Positive:** works on common self-host hardware; updates are predictable; images are verifiable.
- **Negative:** multi-arch builds take longer in CI; release notes become a real step for every server
  release.
- **Follow-ups:** buildx multi-arch in `deploy.yml` (or a separate `release-server.yml`); tag scheme above;
  cosign, provenance and SBOM; make GHCR packages public (after [ADR-0093](0093-publish-the-repository.md));
  our production `.env` switches from `IMAGE_TAG=latest` to a pinned version or `edge`.
- **New risks:** an `arm64`-only bug goes unnoticed — the self-host smoke test
  ([ADR-0095](0095-self-host-deployment-topology.md)) runs on both architectures (native `arm64` runners).

## Pros and cons of the options

### GHCR public, multi-arch, channels, signed
- 👍 Covers the real self-host hardware; safe defaults; verifiable.
- 👎 More CI time and release work.

### Docker Hub too
- 👍 The default registry most people know.
- 👎 Pull-rate limits and a second set of credentials; not needed yet.

### Just make the packages public
- 👍 No work.
- 👎 `:latest` would ship unreleased builds; no `arm64`; nothing to verify.

## Links

- Related ADRs: [ADR-0038](0038-ci-cd-pipeline.md), [ADR-0047](0047-versioning-and-release-strategy.md),
  [ADR-0093](0093-publish-the-repository.md), [ADR-0095](0095-self-host-deployment-topology.md),
  [ADR-0096](0096-client-server-selection-and-discovery.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (IMG-1…IMG-4)
