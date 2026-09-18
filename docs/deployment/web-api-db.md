# Deployment — Web / API / Database

How the server side of the suite is hosted: the Blazor WASM PWA, the Web API, and PostgreSQL. The
provider and topology are decided in [ADR-0036](../adr/0036-cloud-provider-and-hosting.md); the
transactional email sender in [ADR-0037](../adr/0037-transactional-email-provider.md). The other
deployment track — the native heads — is [mobile apps](mobile-apps.md).

> **Status — live (owner-confirmed 2026-09-18).** The stack is **deployed and serving** on the
> Infomaniak VPS: the two WASM PWAs and the API run behind Caddy/TLS (origins reachable over HTTPS),
> **account sign-in and sync work end-to-end**, **transactional email is delivering** (Brevo), and
> **nightly backups are running**. Deployment artifacts live in
> [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy) (`compose.yaml`,
> `api.Dockerfile`, `web.Dockerfile`, `Caddyfile`, `.env.example`, `README.md`) with CD in
> `.github/workflows/deploy.yml`. The milestone-8 checklist below is updated to match. The **restore
> drill has PASSED** (2026-09-17, see [backups](backups.md)). Still owed to fully button up the launch: confirm the
> **DB one-way-door migrations** have run in production (they are applied in code — see
> [launch-readiness §D](launch-readiness.md)) and a **light legal review** of the published legal pages.

## Targets

- **Web app** — standalone Blazor WASM PWA ([ADR-0005](../adr/0005-web-hosting-render-model.md)), static
  files served at the edge / by the reverse proxy.
- **Web API** — ASP.NET (account/session, circles, `/sync`, assignment projection).
- **Database** — PostgreSQL ([ADR-0007](../adr/0007-persistence-approach.md)), self-managed on the host.
- Apps served as **subdomains**: `<app>.<suite-domain>`.
- **Docs site** (DocFX `_site`) — static host / Pages.

## Topology (ADR-0036, ADR-0063)

```
DNS at Infomaniak → A records point straight at the VPS   (ADR-0063: no Cloudflare edge)
        │
        ▼
Infomaniak VPS (Swiss, ~2 vCPU / 4 GB / 60 GB NVMe)
  ├── Caddy  — reverse proxy, Let's Encrypt TLS (HTTP-01), serves the static WASM PWA
  ├── api        (ASP.NET container)   ──►  postgres (container, NVMe volume)
  └── nightly pg_dump  ──►  Infomaniak Swiss Backup (S3, offsite)
```

The whole request path stays in Switzerland ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)):
no CDN/edge proxy sits in front, so TLS is terminated by Caddy on the box.

One small Infomaniak VPS runs the API, Postgres and a reverse proxy under **Docker Compose**. Aspire's
`aspire publish` can emit a Compose manifest, so the deployed shape mirrors the local `AppHost`
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). One production environment; local
Aspire is dev. Managed Postgres (Infomaniak DB service or Exoscale DBaaS) is the documented upgrade path
— a connection-string change — if self-managed backups become a chore.

### Chosen product & sizing (concrete order)

Concrete pick against the ADR-0036 baseline: **Infomaniak VPS Lite M** — 2 vCPU / 4 GB / ~60–80 GB
NVMe, **Geneva (Switzerland)**, **Ubuntu 26.04 LTS** (prefer the `26.04.1` point-release image;
fall back to **24.04 LTS** only if Infomaniak's picker does not yet offer 26.04); ~CHF 8–10 / mo, no
commitment. This matches the baseline spec above, so it is a concrete confirmation of ADR-0036, not a
new decision. 26.04 LTS is chosen over 24.04 for the longer support runway (standard ~2031 / ESM ~2036);
it is a Docker host, so the OS userland surface is minimal and Docker 29 is pinned for the LTS.

Sizing rationale: the VPS only *runs* containers (images are built in GitHub Actions and pulled from
GHCR), so 4 GB is not stressed by builds; Postgres 17 + the .NET API + Caddy fit at launch traffic.
VPS Lite resizes **in place** (Manager → ⋮ → change configuration; ~20 min downtime, no reprovision,
no new IP, no data migration). **Upgrade-to-L trigger:** sustained memory pressure, or a second
high-traffic suite app going live → VPS Lite L (4 vCPU / 8 GB / 160 GB).

## Costs (CHF, ex-VAT; CHF/EUR near parity, treat as ±10 %)

| Piece | Provider | Spec | Cost |
|---|---|---|---|
| Domain `.ch` | Infomaniak | registration + renewal | ~CHF 11 / yr |
| Compute | Infomaniak VPS Lite | 2 vCPU / 4 GB / 60 GB NVMe, unlimited traffic | ~CHF 8–9 / mo |
| Offsite backup | Infomaniak Swiss Backup | 200 GB (1 TB = 4.18) | CHF 2.40 / mo |
| TLS | Caddy + Let's Encrypt | on the VPS (HTTP-01, ADR-0063) | CHF 0 |
| Transactional email | Brevo Free ([ADR-0037](../adr/0037-transactional-email-provider.md)) | ~9,000 mails/mo | CHF 0 |
| Role mailbox `dev@<brand>` | Infomaniak Mail | 1 custom-domain mailbox | ~CHF 0–1.50 / mo |

**Recurring web/cloud ≈ CHF 12–14 / mo (~CHF 150 / yr).** The store fees are in the
[mobile runbook](mobile-apps.md); iOS now builds on **hosted macOS CI** at ~CHF 0
([ADR-0065](../adr/0065-hosted-macos-ios-builds.md), superseding [ADR-0031](../adr/0031-ios-build-host.md)),
so no Mac purchase is on the shipping critical path.

**Alternatives considered** (ADR-0036): Infomaniak Jelastic PaaS (dearer for steady tiny load),
Public Cloud + managed Postgres (+~CHF 20–30/mo), managed Kubernetes and hyperscalers (both
disproportionate). cloudscale.ch is a strong pure-Swiss IaaS but its smallest VM (~CHF 33/mo) is dearer
than Infomaniak VPS Lite here; its object storage (CHF 0.03/GB/day) is a good backup target if ever
needed.

## Open sub-decisions (still to record)

- **CI/CD pipeline** — ✅ decided in [ADR-0038](../adr/0038-ci-cd-pipeline.md) (GitHub Actions). The CI
  gate (build/test/docs + MAUI heads) is built; the **CD** step is **scaffolded** in
  `.github/workflows/deploy.yml` (build → GHCR → SSH `compose pull` → one-shot `migrator` → `up -d`),
  manual-trigger only until the VM and `DEPLOY_*` secrets exist. EF Core migrations are the explicit
  release step (`docker compose run --rm migrator`, i.e. the API image run with `--migrate`).
- **Secrets management** — ✅ decided in [ADR-0050](../adr/0050-secrets-management.md) (Accepted
  2026-08-25): two-tier — GitHub Actions encrypted secrets for CI/deploy + a git-ignored `deploy/.env`
  on the host for runtime (DB credentials, JWT signing key, Brevo key), with the authoritative copy in a
  password manager for recovery/rotation; SOPS+age recorded as the upgrade path. Never in the repo. The
  provisioning order, per-secret rotation table and single-VM recovery drill are in
  [Secrets & recovery](#secrets--recovery-adr-0050) below.
- **Production observability backend** — ✅ decided in
  [ADR-0051](../adr/0051-production-observability-backend.md) (Accepted 2026-08-25): self-hosted **Loki +
  Prometheus + Grafana** on the VPS. Prometheus scrapes the API's internal `/metrics`; the API ships
  OTLP logs to Loki; **Grafana** is the persistent admin view at `grafana.circletools.ch` behind its own
  login. Prometheus/Loki are never published; telemetry stays on-box. See
  [Observability](#observability-adr-0051) below.

## Secrets & recovery (ADR-0050)

Which secrets exist, where each **value** durably lives, how to provision the box, rotate any one of
them, and rebuild from nothing. The policy is [ADR-0050](../adr/0050-secrets-management.md): the
**authoritative** copy of every value is an entry in the maintainer's **password manager (vault)**;
`deploy/.env` on the VM and the GitHub Actions secrets are **derived copies**. Nothing secret is ever in
git — [`deploy/.env.example`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
records *which* values exist, not the values.

### The two tiers

| Tier | Where it lives | Values | Consumer |
|---|---|---|---|
| **Runtime / host** | `/opt/circletools/.env` on the VM (`chmod 600`, owned by the deploy user; git-ignored) | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `ConnectionStrings__maindb` (password in lockstep), `Auth__Jwt__SigningKey` ([ADR-0021](../adr/0021-web-auth-token-model.md)), `Mail__Username`, `Mail__Password` ([ADR-0037](../adr/0037-transactional-email-provider.md)), `GRAFANA_ADMIN_PASSWORD` ([ADR-0051](../adr/0051-production-observability-backend.md)), `RESTIC_PASSWORD`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` ([backups](backups.md)) | `compose.yaml` → `api` / `migrator` / `postgres` / `grafana`; **`backup.sh`** (systemd timer) |
| **CI / deploy** | GitHub → Settings → Secrets and variables → Actions (repository encrypted secrets) | `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`; `GITHUB_TOKEN` (built-in, GHCR push) | `.github/workflows/deploy.yml` (SSH rollout) |

CI holds **only deploy access** — never a runtime secret. Postgres is not published off the internal
Docker network, so the DB credentials never leave the box. `ACME_EMAIL` and `IMAGE_TAG` in `.env` are
config, not secrets.

### Provisioning order (vault first, then derive)

Generate every value **into the vault first**, then copy it out onto the host / into GitHub — never the
other way round.

1. **Generate + store in the vault:**
   - `POSTGRES_PASSWORD` — strong random (e.g. `openssl rand -base64 32`).
   - `Auth__Jwt__SigningKey` — `openssl rand -base64 48` (≥ 32 bytes; a leaked key mints sessions for any account).
   - Brevo SMTP **login + key** — from the Brevo dashboard ([ADR-0037](../adr/0037-transactional-email-provider.md)).
   - Deploy SSH keypair — `ssh-keygen -t ed25519 -C circletools-deploy` (passphrase-protected); private key → vault.
   - Backup ([backups](backups.md)): Swiss Backup S3 access (`AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`) from the Infomaniak dashboard, and `RESTIC_PASSWORD` = `openssl rand -base64 32` — a **durable** secret (losing it strands every backup). These are **runtime/host**, not CI: the backup runs on the VM via a systemd timer, so they go in `.env`, never in GitHub Actions.
2. **Derive the host tier:** on the VM, copy `deploy/.env.example` → `/opt/circletools/.env`, fill from the
   vault, keep the `ConnectionStrings__maindb` password **identical** to `POSTGRES_PASSWORD`, then
   `chmod 600 /opt/circletools/.env` and `chown` it to the deploy user.
3. **Derive the CI tier:** add `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` (the private key),
   `DEPLOY_KNOWN_HOSTS` (`ssh-keyscan <host>`) as GitHub Actions secrets; append the deploy **public** key
   to the VM user's `~/.ssh/authorized_keys`. `GITHUB_TOKEN` is built-in — nothing to create.
4. Setting **`DEPLOY_HOST`** is the switch that self-activates the rollout job in `deploy.yml` — no edit
   to the workflow needed.

### Rotation

Every secret has a written path. In all cases: **edit the vault first**, then the derived copy, then
redeploy.

| Secret | When | Steps |
|---|---|---|
| **DB password** | Suspected leak; scheduled hygiene | Update in vault → edit `POSTGRES_PASSWORD` **and** the `ConnectionStrings__maindb` password in `.env` (lockstep) → `docker compose up -d` (Postgres re-reads on restart). |
| **JWT signing key** ([ADR-0021](../adr/0021-web-auth-token-model.md)) | Suspected compromise only | Update in vault → edit `Auth__Jwt__SigningKey` → `up -d`. **Invalidates all live sessions by design** — everyone re-signs-in; accept that cost. |
| **Brevo SMTP key** ([ADR-0037](../adr/0037-transactional-email-provider.md)) | Leak; Brevo reissue | Reissue in the Brevo dashboard → update vault → edit `Mail__Username` / `Mail__Password` → `up -d`. |
| **Grafana admin password** ([ADR-0051](../adr/0051-production-observability-backend.md)) | Leak; scheduled hygiene | Change in vault → edit `GRAFANA_ADMIN_PASSWORD` → `docker compose up -d grafana` (or reset in the Grafana UI and update the vault to match). |
| **Deploy SSH key** | Leak; maintainer offboard | `ssh-keygen` a new pair → update the `DEPLOY_SSH_KEY` GitHub secret and vault → swap the public key in the VM's `authorized_keys` → remove the old line. |
| **Backup S3 keys** ([backups](backups.md)) | Leak; scheduled hygiene | Reissue the Swiss Backup S3 access in the Infomaniak dashboard → update vault → edit `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` in `.env`. No restart needed (read per run). |
| **restic password** ([backups](backups.md)) | Never rotate casually | `RESTIC_PASSWORD` is **durable** — it decrypts every existing backup. Use `restic key add` to add a new key *without* invalidating old snapshots, update the vault + `.env`, then `restic key remove` the old one. Never just overwrite it. |
| **Mobile store signing / API keys** (future) | When the upload legs are wired ([ADR-0030](../adr/0030-mobile-store-publisher-identity.md), [ADR-0047](../adr/0047-versioning-and-release-strategy.md)) | Join the **CI tier** as GitHub Actions secrets under this same policy — see the [mobile runbook](mobile-apps.md). |

### Single-VM recovery drill

The VM is a single point of failure ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md)); losing it
must not lose the secrets. Because the authoritative copy is the vault and the data is in the nightly
`pg_dump` on Swiss Backup, recovery is a fixed sequence:

1. **Reprovision** a VPS (VPS Lite M, Geneva, Ubuntu LTS), install Docker + Compose, create the deploy
   user, restore its `authorized_keys` public key.
2. **Restore the code / compose stack** — clone the repo (or pull the pinned images from GHCR).
3. **Recreate `/opt/circletools/.env`** from the vault (step 2 of provisioning above), `chmod 600`.
4. **Restore the database** — start `postgres`, then `bash ./restore.sh --target prod` loads the latest
   Swiss Backup snapshot into `maindb` (see [Backups & restore](backups.md)).
5. **Roll forward** — `docker compose run --rm migrator` (applies any pending EF migrations), then
   `docker compose up -d`.
6. **Re-point the Infomaniak DNS** A records at the new IP if it changed; Caddy re-issues TLS on boot.
7. **Update CI** — if the host or its key changed, refresh `DEPLOY_HOST` / `DEPLOY_KNOWN_HOSTS`.

No step needs anything that only existed on the lost box: values come from the vault, data from the
offsite dump. **Test the restore** (checklist below) so this drill is exercised, not just written.

> **Upgrade path (trigger-gated).** ADR-0050 records **SOPS + age** — encrypted secrets committed to git
> — as the upgrade to adopt (as a superseding ADR) if a second maintainer joins, the secret count/rotation
> cadence outgrows "edit the vault, edit `.env`", or plaintext-at-rest on the VM must go. Not now.

## Observability (ADR-0051)

Self-hosted **Loki + Prometheus + Grafana**, on the same VPS, all on the internal Docker network. Only
**Grafana** is exposed; Prometheus and Loki have no Caddy site block and are never published. Telemetry
stays on the box. Scope today is **logs + metrics** (no traces/alerting yet — additive later).

| Piece | Role | Reached by |
|---|---|---|
| **Prometheus** (`prom/prometheus`) | **Scrapes** the API's `/metrics` over the Docker network (`api:8080/metrics`), 30-day TSDB | Grafana only (internal) |
| **Loki** (`grafana/loki`) | Stores the API's logs, ingested via **OTLP** (`api` → `http://loki:3100/otlp`), 30-day retention | Grafana only (internal) |
| **Grafana** (`grafana/grafana`) | The admin view; datasources **and** the overview dashboard auto-provisioned | `https://grafana.circletools.ch`, Grafana admin login |
| **node_exporter** (`prom/node-exporter`) | Host metrics — CPU / RAM / disk / filesystem / network / load | Prometheus only (internal) |
| **cAdvisor** (`gcr.io/cadvisor/cadvisor`) | Per-container CPU / memory (API vs Postgres vs the stack itself) | Prometheus only (internal) |

**Dashboard.** Grafana provisions **"CircleTools — Service & Host"**
([`circletools-overview.json`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/grafana/provisioning/dashboards))
— at-a-glance tiles (API up, req/s, error rate, p95, host CPU/RAM/disk), then rows for **API** (request
rate by status, latency p50/95/99, in-flight, sync collisions), **.NET runtime** (GC, process CPU/mem,
threadpool), **Host** (CPU/mem/disk/net/load from node_exporter), **Containers** (per-container CPU/mem
from cAdvisor), and **Logs** (Loki log-rate + a live logs panel). It loads automatically; edits made in
the UI can be exported back into that JSON to keep them in git.

**How it is wired.** `CircleTools.ServiceDefaults` exposes the Prometheus scraping endpoint at `/metrics`
(all environments) and — when `Observability__LokiOtlpEndpoint` is set (it is, in `compose.yaml`) — adds
an OTLP **log** exporter to Loki. Local dev sets neither Loki nor a public `/metrics`, so it is
unaffected (Aspire keeps its dashboard). Config lives in
[`deploy/prometheus/prometheus.yml`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/prometheus),
[`deploy/loki/loki-config.yaml`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/loki),
and [`deploy/grafana/provisioning/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/grafana).

**Security.** `/metrics` (and `/alive`) are **denied on the public `api.circletools.ch` origin** by
Caddy — Prometheus reaches them only internally. Grafana enforces its own admin login
(`GRAFANA_ADMIN_PASSWORD`), sign-up and anonymous access disabled.

**Log floor.** The Docker `json-file` driver stays on (rotated `max-size`/`max-file`), so
`docker compose logs` still works on the box even when Grafana/Loki is down. Prometheus/Loki data volumes
are expendable (retention-bounded); back up **Grafana's** config/dashboards.

**Provisioning steps (host-side).**
1. ✅ **`grafana.circletools.ch`** DNS record created (mind the HTTP-01 caveat as the other origins —
   see [`deploy/README.md`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)).
2. ✅ **`GRAFANA_ADMIN_PASSWORD`** generated (`openssl rand -base64 24`) and set in the VM's `deploy/.env`
   ([Secrets & recovery](#secrets--recovery-adr-0050)) — record the value in the vault as the
   authoritative copy.
3. ✅ Brought up and **smoke-tested end-to-end (2026-08-25)**: Grafana loads at `grafana.circletools.ch`
   (admin login), both datasources green, Prometheus scraping the API (`up{job="api"}` = 1), and the API
   shipping OTLP logs to Loki (the `service_name` label is present; query `{service_name=~".+"}`).

> **Deploy mechanics (now handled by the pipeline).** `deploy.yml` **rsyncs** `compose.yaml`, the
> observability config, and the [backup scripts/units](backups.md) to the VM on every deploy (never
> `.env`) and **pins the deployed `IMAGE_TAG`** in
> the VM's `.env`. This closes the two traps hit during first bring-up: config no longer has to be
> hand-`scp`'d, and a manual `docker compose up` can't drift to a stale `:latest` (which had run
> pre-observability code). Also fixed permanently: the API's OTLP log endpoint is the **full** signal path
> `…/otlp/v1/logs` — the .NET OTLP/HTTP exporter, set programmatically, does not append `/v1/logs`, so a
> bare `/otlp` silently 404s at Loki.

**Upgrades (ADR-0051):** add **Tempo** for traces, **alert rules**, an all-container **log shipper**, or
move to **Grafana Cloud** if the on-box footprint ever hurts.

## Checklist (milestone 8)

The stack is **live** (owner-confirmed 2026-09-18), so the operational items below are `[x]`; two
verifications are still hedged inline (the restore *drill* and the legal review).

- [x] Brand & domain final; domain registered at Infomaniak (`circletools.ch`, 2026-08-13)
- [x] Project-owned Infomaniak account (not a personal login) for host + backup
- [x] VPS provisioned (**VPS Lite M**, Geneva, Ubuntu LTS); Docker + Compose
- [x] Reverse proxy with Let's Encrypt TLS — `Caddyfile` (three origins) via the default **HTTP-01
  challenge** (stock `caddy:2-alpine`); no proxy in front, so `:80` reaches Caddy directly ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)); origins served over HTTPS
- [x] DNS at Infomaniak: A records for the three origins (+ `grafana`) point at the VPS ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) — no Cloudflare)
- [x] Passkey RP-ID + API CORS baked to `circletools.ch` (`Main.Service/appsettings.Production.json`); PWA `ApiBaseUrl`/`SsoAuthorityUrl` baked (each `wwwroot/appsettings.Production.json`); Secret-Santa `RevealBaseUrl` = own origin
- [x] CSP directive set — full policy shipped in `Caddyfile` (ADR-0021/0040/0048), **statically verified against both WASM heads' assets** and **boot-smoke'd in real Chromium** (2026-08-25, 0 CSP violations), and now **running in production**: sign-in/sync exercise the real cross-origin `connect-src` fetch to `api.circletools.ch` (the last-word live pin). Dev `dotnet run` serves no CSP by design.
- [x] Brevo behind the SMTP sender — **transactional email delivering in production**; account + SPF / DKIM / DMARC on the domain
- [x] Postgres container on an NVMe volume; **nightly `pg_dump` → Swiss Backup** running
  (`deploy/backup.sh` via restic + `systemd/circletools-backup.timer`; [runbook](backups.md)); the
  **`deploy/restore.sh` restore drill has PASSED end-to-end** (2026-09-17, [backups](backups.md)) — restore is proven.
- [x] EF Core migrations as an explicit release step — one-shot `migrator` service (`--migrate`), applied in production
- [x] Privacy policy + imprint + account-deletion URLs published, reachable, DE/EN/FR — live at `secretsanta.circletools.ch/{privacy,imprint,delete-account}`. *WASM-rendered (see the client-side caveat in the [mobile runbook](mobile-apps.md)); a **light legal review** of the wording is still owed.*
- [x] Secrets: `.env.example` template + git-ignored `.env`; host/CI store per [ADR-0050](../adr/0050-secrets-management.md), provisioned from the vault (see the [drill](#secrets--recovery-adr-0050))
- [x] Observability backend **live ([ADR-0051](../adr/0051-production-observability-backend.md))** — self-hosted Loki + Prometheus + Grafana on the VPS; Grafana at `grafana.circletools.ch` (admin login), Prometheus scraping the API (`up`=1), the API shipping OTLP logs to Loki; **verified end-to-end 2026-08-25** (see [Observability](#observability-adr-0051))
- [x] Deploy & smoke-test done — sign-in / sync / email verified in production; single-VM recovery (rebuild + restore) documented — [drill](#secrets--recovery-adr-0050) — and the **restore drill has PASSED** (2026-09-17, [backups](backups.md))

Not on this list, still open: the **DB one-way-door schema fixes** — tracked in
[launch-readiness §D](launch-readiness.md).

## Related

- [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) — provider & topology
- [ADR-0037](../adr/0037-transactional-email-provider.md) — transactional email
- [Mobile apps](mobile-apps.md) — the other deployment track
- [arc42 ch. 7](../arc42/07-deployment-view.md) — deployment view
