# Container images

How the server images are built, tagged, released and verified
([ADR-0098](../adr/0098-container-image-distribution.md)). The self-hosting guide (backlog SH-9) builds on
this page.

## The images

| Image | What it contains |
|-------|------------------|
| `ghcr.io/circletools-ch/circletools-api` | The API (accounts, circles, sync, Secret-Santa). Also the migrator (`--migrate`). Runs as the non-root `app` user on port 8080. |
| `ghcr.io/circletools-ch/circletools-web` | Caddy serving the hub and Secret-Santa web apps, proxying the API. Still runs as root for TLS on 80/443; non-root comes with the self-host plain-HTTP mode ([ADR-0095](../adr/0095-self-host-deployment-topology.md), SH-2). |

Both are built for **`linux/amd64` and `linux/arm64`** (Raspberry Pi 4/5, ARM NAS boxes, Apple-silicon
Docker). Base images are pinned by digest and refreshed by Dependabot.

## Tags

| Tag | Moves when | Use it for |
|-----|-----------|------------|
| `:<version>` (e.g. `1.0.30`) | never — one per build | pinning; our own server always runs one |
| `:<commit-sha>` | never | tracing an image back to its commit |
| `:edge` | every build from `main` (Deploy, `deploy.yml`) | trying the newest build |
| `:stable` = `:latest` | a server release | self-hosting with the least surprise |

There are no `:1` / `:1.0` tags: versions are `<major>.<minor>.<git-height>`, so the minor version stays the
same for months and such a tag would say no more than `:stable`.

## Making a server release

A server release promotes images that **Deploy already built and our server already ran** — it never
rebuilds them.

1. Pick the version to release: one that Deploy built from `main` and that has run well on our server
   (the `IMAGE_TAG` in the VM's `.env`, or the Deploy run's summary).
2. Find its commit: the image's `:<commit-sha>` tag, or `docker buildx imagetools inspect
   ghcr.io/circletools-ch/circletools-api:<version>` and the `org.opencontainers.image.revision` label.
3. On GitHub → *Releases* → *Draft a new release*: tag **`server-v<version>`** (for example
   `server-v1.0.30`) on that commit. **Not `v…`** — a `v*` tag starts the mobile store release.
4. Write the notes: database migrations, configuration changes (new or renamed `.env` values), breaking
   changes, the minimum app version this server supports and the minimum server version the current apps
   need ([ADR-0096](../adr/0096-client-server-selection-and-discovery.md)).
5. *Publish release.* *Release (server)* (`release-server.yml`) checks that
   `:<version>` and `:<commit-sha>` are the same image for both the API and the web image, then re-tags it
   as `:stable` and `:latest`. A *pre-release* does not move them.

## Verifying an image

Every image is signed with cosign (keyless, by this repository's Deploy workflow) and carries SLSA
provenance and an SPDX SBOM. To check that an image was built by our CI from this repository:

```bash
cosign verify ghcr.io/circletools-ch/circletools-api:stable \
  --certificate-identity-regexp '^https://github.com/CircleTools-ch/circletools-suite/\.github/workflows/deploy\.yml@refs/heads/main$' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

The provenance and the SBOM:

```bash
docker buildx imagetools inspect ghcr.io/circletools-ch/circletools-api:stable --format '{{ json .Provenance }}'
docker buildx imagetools inspect ghcr.io/circletools-ch/circletools-api:stable --format '{{ json .SBOM }}'
```

## Our own server

Every Deploy rollout writes `IMAGE_TAG=<version>` into the VM's `.env`, so the hosted service always runs
an exact version, usually newer than `:stable`. The fallback in `deploy/compose.yaml` is `:edge`, never
`:latest`, so a manual `docker compose` without `IMAGE_TAG` cannot jump back to an older release.
