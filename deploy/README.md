# Deployment scaffold — Infomaniak VPS + Docker Compose

Production deployment artifacts for the CircleTools suite. Topology and rationale:
[ADR-0036](../docs/adr/0036-cloud-provider-and-hosting.md); suite composition (one origin + one
listing per app): [ADR-0046](../docs/adr/0046-suite-packaging-and-app-composition.md); CI/CD:
[ADR-0038](../docs/adr/0038-ci-cd-pipeline.md). The narrative runbook is
[`docs/deployment/web-api-db.md`](../docs/deployment/web-api-db.md).

> **Status: scaffold, not yet live.** No VPS exists yet and secrets are unset. The files here build and
> run locally against the real domain config; going live is the milestone-8 checklist below.

## What's here

| File | Role |
|------|------|
| `compose.yaml` | The stack: `postgres`, a one-shot `migrator`, `api`, and `web` (Caddy). |
| `api.Dockerfile` | Builds the Main API (`CircleTools.Main.Service`). |
| `web.Dockerfile` | Builds both WASM PWAs and bakes them into a Caddy image. |
| `Caddyfile` | TLS termination, static serving of both PWAs, reverse proxy to the API. |
| `.env.example` | Every secret/host value the stack needs. Copy to `.env` (git-ignored) on the host. |

## Origins (federated suite — ADR-0046)

| Origin | Served by | Notes |
|--------|-----------|-------|
| `https://circletools.ch` | `web` → `/srv/main` | Main hub; SSO authority; passkey **RP-ID = `circletools.ch`**. |
| `https://secretsanta.circletools.ch` | `web` → `/srv/secretsanta` | Secret-Santa PWA. |
| `https://api.circletools.ch` | `web` → `reverse_proxy api:8080` | ASP.NET API. |

Client origins are baked from each app's `wwwroot/appsettings.Production.json` (public config, no
secrets). The API's CORS / SSO return-uri allow-list lives in
`CircleTools.Main.Service/appsettings.Production.json` — **keep the two in step**.

## Run it locally (against prod config)

```bash
cp deploy/.env.example deploy/.env      # then edit deploy/.env
docker compose -f deploy/compose.yaml --env-file deploy/.env build
docker compose -f deploy/compose.yaml --env-file deploy/.env run --rm migrator
docker compose -f deploy/compose.yaml --env-file deploy/.env up -d
```

Caddy will try to obtain real Let's Encrypt certs for `circletools.ch` — for a pure local smoke test,
point those names at `127.0.0.1` in `hosts` and expect cert warnings, or test the API image alone.

## Migrations

The API image is also the migrator. `docker compose run --rm migrator` runs it with `--migrate`, which
applies all four EF Core contexts' migrations and **exits** without starting the web host
(`Main.Service/Program.cs`). A normal `api` boot never migrates in production — the deploy runs the
migrator first (the CD workflow and the commands above both do).

## Image versioning & the deploy trigger (ADR-0047)

Images are versioned automatically from git — **Nerdbank.GitVersioning** stamps `0.1.<git-height>`
(from the root [`version.json`](../version.json)) into the assemblies, and
[`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) tags each pushed image
`:<version>`, `:<commit-sha>`, and `:latest`. The version reaches the image via a `VERSION` build-arg
(the build context omits `.git`), so the Dockerfiles here publish with `-p:ContainerBuild=true
-p:Version=$VERSION`.

**Trigger:** deploy runs after the CI gate succeeds on `main` (and on manual dispatch for
redeploy/rollback). It **always builds and pushes** the versioned images to GHCR; the **SSH rollout job
stays skipped until `DEPLOY_HOST` is set**, then self-activates — no edit needed. Set `IMAGE_TAG` in
`.env` (or let the workflow pass it) to pin which tag the stack runs. Mobile store releases are a
*separate*, tag-triggered workflow (`release-mobile.yml`), not part of this server track.

## Known follow-ups before going live

- **Secrets management (decided — ADR-0050).** `.env` on the host + GitHub Actions secrets for CI is
  ratified by [ADR-0050](../docs/adr/0050-secrets-management.md) (Accepted 2026-08-25), with the
  authoritative copy of every value in a password manager for recovery/rotation and SOPS+age as the
  upgrade path. Nothing secret is committed.
- **Cloudflare + ACME.** With Cloudflare's proxy ON, Let's Encrypt HTTP-01 is intercepted. Issue certs
  with the record in **DNS-only** first, or switch Caddy to the **Cloudflare DNS-01** challenge
  (`caddy-dns/cloudflare` build + a scoped token). See the note in `Caddyfile`.
- **Content-Security-Policy.** The `Caddyfile` now ships the finalized CSP as a response header for
  both WASM PWAs (ADR-0021, ADR-0040): `script-src 'self' 'wasm-unsafe-eval'`,
  `connect-src 'self' https://api.circletools.ch`, `frame-ancestors 'none'`. It is the single source
  of truth — the apps no longer carry a `<meta>` policy. The SSO hand-off (ADR-0039) is a top-level
  navigation to the authority, not a fetch, so the authority origin is deliberately not in
  `connect-src`. Re-verify in-browser (console clean) after any change to the app's outbound calls.
- **Reverse-proxy forwarded headers.** The API sits behind Caddy (TLS terminated). If `Request.IsHttps`
  / scheme-sensitive behaviour (Secure cookies, absolute redirect URIs) misbehaves, add
  `ForwardedHeaders` middleware with the proxy as a known network.
- **Backups.** Nightly `pg_dump` → Infomaniak Swiss Backup, **restore tested** (ADR-0036). Not yet
  scripted here.
- **Observability (live — ADR-0051).** Self-hosted **Loki + Prometheus + Grafana** in `compose.yaml`
  (config under `deploy/prometheus`, `deploy/loki`, `deploy/grafana`). Prometheus scrapes the API's
  internal `/metrics`, the API ships OTLP logs to Loki, and **Grafana** (`grafana.circletools.ch`, own
  login) auto-provisions both datasources **and the "CircleTools — Service & Host" dashboard**. Host
  CPU/RAM/disk/network come from **node_exporter** and per-container CPU/memory from **cAdvisor** (both
  internal-only). Prometheus/Loki are never published; Caddy denies `/metrics` on the public API origin.
- **CD config sync (done).** `deploy.yml` **rsyncs** `compose.yaml` + the observability config to the VM
  on every deploy (never `.env`) and **pins `IMAGE_TAG`** in the VM's `.env` — so config changes ship
  automatically and a manual `docker compose` can't drift to a stale `:latest`. No more hand-`scp`.

## Milestone-8 go-live checklist

See [`docs/deployment/web-api-db.md`](../docs/deployment/web-api-db.md) — the authoritative checklist.
