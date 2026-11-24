---
status: Accepted
date: 2026-11-13
deciders: [Adrian]
tags: [deployment, docker, release, supply-chain, self-hosting]
---

# ADR-0098 — Public container images: multi-arch, release channels, signed

> **Accepted 2026-11-20** by the owner, as revised the same day.
>
> **Revised 2026-11-20** (review against the code): server releases use `server-v*` tags
> because `v*` starts the store release; no `:<major>` / `:<major>.<minor>` tags; a release re-tags
> existing images; non-root details per image; the production pin already exists, only the compose
> fallback changes.

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
- **Architectures:** `linux/amd64` and `linux/arm64` via buildx. The SDK stage runs on
  `--platform=$BUILDPLATFORM`, so `arm64` never builds under emulation. This is cheap because neither
  output depends on the CPU: the API is published framework-dependent (portable IL, no runtime identifier)
  and the web apps are WASM and static files. Only the runtime stage (`aspnet`, `caddy`) differs per
  architecture.
- **Tags:**
  - `:<version>` and `:<commit-sha>` for every build (as today);
  - `:edge` — the last build from `main` (what our server runs);
  - `:stable` — only for a **server release** (see below);
  - `:latest` is an alias of `:stable`, so a naive pull gets a release, not a work-in-progress build.
  - **No `:<major>` or `:<major>.<minor>` tags.** Versions are `<major>.<minor>.<git-height>` (nbgv,
    [ADR-0047](0047-versioning-and-release-strategy.md)), so `1.0` stays the same for months and such a
    tag would carry no more meaning than `:stable`. Revisit if the suite ever versions the server on its
    own with real minor releases.
- **Releases:** a server release is a git tag **`server-v<version>`** (for example `server-v1.0.30`) on a
  commit of `main` whose images were already built, plus a GitHub Release with notes covering: migrations,
  configuration changes, breaking changes, and the minimum app version it supports and the minimum server
  version the current apps need ([ADR-0096](0096-client-server-selection-and-discovery.md)). The release
  re-tags the existing `:<version>` images as `:stable` / `:latest`; it does not rebuild them, so what is
  released is exactly what our server ran. **Not `v*`:** a `v*` tag push starts the mobile store release
  (`release-mobile.yml`), so server and store releases must not share a tag pattern.
- **Supply chain:** images signed with **cosign** (keyless, GitHub OIDC, `id-token: write` on the
  publishing job only), with **SLSA provenance** and an **SBOM** attached by `docker/build-push-action`.
  The self-hosting guide shows the `cosign verify` command.
- **Base images** pinned by digest and refreshed by Dependabot (a `docker` ecosystem entry next to today's
  `github-actions` one in `.github/dependabot.yml`).
- **Non-root:** the API image runs as the `aspnet` image's built-in `app` user (`USER $APP_UID`); it
  already listens on 8080. The web image runs Caddy as a non-root user listening on 8080 in the plain-HTTP
  mode of [ADR-0095](0095-self-host-deployment-topology.md); only the stand-alone TLS mode (and our hosted
  service) binds 80/443, which needs `cap_net_bind_service` on the Caddy binary instead of root.

## Consequences

- **Positive:** works on common self-host hardware; updates are predictable; images are verifiable.
- **Negative:** multi-arch builds take longer in CI; release notes become a real step for every server
  release.
- **Follow-ups:** buildx multi-arch in `deploy.yml`, which pushes `:<version>`, `:<commit-sha>` and
  `:edge` (no longer `:latest`); a separate `release-server.yml` on `server-v*` tags that re-tags and
  writes the GitHub Release; cosign, provenance and SBOM; non-root users; the Dependabot `docker` entry;
  make GHCR packages public (after [ADR-0093](0093-publish-the-repository.md)).
  Our production server already runs a pinned version: every rollout writes `IMAGE_TAG=<version>` into
  the VM's `.env`. The fallback in `deploy/compose.yaml` (`${IMAGE_TAG:-latest}`) changes to `edge` in the
  same change that stops pushing `:latest` from `deploy.yml`, so a manual `docker compose` without the
  variable never jumps back to an older release.
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
