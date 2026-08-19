# Deployment — Web / API / Database

How the server side of the suite is hosted: the Blazor WASM PWA, the Web API, and PostgreSQL. The
provider and topology are decided in [ADR-0036](../adr/0036-cloud-provider-and-hosting.md); the
transactional email sender in [ADR-0037](../adr/0037-transactional-email-provider.md). The other
deployment track — the native heads — is [mobile apps](mobile-apps.md).

> **Status:** decided; **Compose stack scaffolded** (2026-08-19), not yet live on a VPS. The
> deployment artifacts live in [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
> (`compose.yaml`, `api.Dockerfile`, `web.Dockerfile`, `Caddyfile`, `.env.example`, `README.md`) with
> the CD workflow in `.github/workflows/deploy.yml` (manual-trigger until the VPS and secrets exist).
> The brand & domain gate is now **closed** — `circletools.ch` is registered, so the production origins
> are baked: the API's passkey RP-ID / CORS in `Main.Service/appsettings.Production.json`, and each
> PWA's `ApiBaseUrl` / `SsoAuthorityUrl` in its `wwwroot/appsettings.Production.json`. What remains is
> provisioning the VPS, the secrets store, the CSP, and backups (below).

## Targets

- **Web app** — standalone Blazor WASM PWA ([ADR-0005](../adr/0005-web-hosting-render-model.md)), static
  files served at the edge / by the reverse proxy.
- **Web API** — ASP.NET (account/session, circles, `/sync`, assignment projection).
- **Database** — PostgreSQL ([ADR-0007](../adr/0007-persistence-approach.md)), self-managed on the host.
- Apps served as **subdomains**: `<app>.<suite-domain>`.
- **Docs site** (DocFX `_site`) — static host / Pages.

## Topology (ADR-0036)

```
Cloudflare (Free: DNS, TLS, CDN, DDoS, basic WAF)
        │
        ▼
Infomaniak VPS (Swiss, ~2 vCPU / 4 GB / 60 GB NVMe)
  ├── Caddy (or Traefik)  — reverse proxy, Let's Encrypt TLS, serves the static WASM PWA
  ├── api        (ASP.NET container)   ──►  postgres (container, NVMe volume)
  └── nightly pg_dump  ──►  Infomaniak Swiss Backup (S3/Swift, offsite)
```

One small Infomaniak VPS runs the API, Postgres and a reverse proxy under **Docker Compose**. Aspire's
`aspire publish` can emit a Compose manifest, so the deployed shape mirrors the local `AppHost`
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). One production environment; local
Aspire is dev. Managed Postgres (Infomaniak DB service or Exoscale DBaaS) is the documented upgrade path
— a connection-string change — if self-managed backups become a chore.

## Costs (CHF, ex-VAT; CHF/EUR near parity, treat as ±10 %)

| Piece | Provider | Spec | Cost |
|---|---|---|---|
| Domain `.ch` | Infomaniak | registration + renewal | ~CHF 11 / yr |
| Compute | Infomaniak VPS Lite | 2 vCPU / 4 GB / 60 GB NVMe, unlimited traffic | ~CHF 8–9 / mo |
| Offsite backup | Infomaniak Swiss Backup | 200 GB (1 TB = 4.18) | CHF 2.40 / mo |
| CDN / TLS / DDoS / WAF | Cloudflare Free | edge proxy | CHF 0 |
| Transactional email | Brevo Free ([ADR-0037](../adr/0037-transactional-email-provider.md)) | ~9,000 mails/mo | CHF 0 |
| Role mailbox `dev@<brand>` | Infomaniak Mail | 1 custom-domain mailbox | ~CHF 0–1.50 / mo |

**Recurring web/cloud ≈ CHF 12–14 / mo (~CHF 150 / yr).** The store fees and the iOS build Mac are in
the [mobile runbook](mobile-apps.md); the Mac ([ADR-0031](../adr/0031-ios-build-host.md)) is the single
largest cost of the whole shipping story.

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
- **Secrets management** — DB credentials, the Brevo API key, signing assets; host/CI secret store,
  never in the repo.
- **Production observability backend** — the Aspire dashboard
  ([ADR-0012](../adr/0012-observability-and-telemetry.md)) is dev-only.

## Checklist (milestone 8)

Scaffolded items (`[~]`) have their artifacts in [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy); they still need a live VPS + secrets to be `[x]`.

- [x] Brand & domain final; domain registered at Infomaniak (`circletools.ch`, 2026-08-13)
- [ ] Project-owned Infomaniak account (not a personal login) for host + backup
- [ ] VPS provisioned; Docker + Compose
- [~] Reverse proxy with Let's Encrypt TLS — `Caddyfile` (three origins; Cloudflare/ACME caveat noted)
- [ ] Cloudflare in front (DNS, proxy, TLS); WAF baseline
- [x] Passkey RP-ID + API CORS baked to `circletools.ch` (`Main.Service/appsettings.Production.json`); PWA `ApiBaseUrl`/`SsoAuthorityUrl` baked (each `wwwroot/appsettings.Production.json`); Secret-Santa `RevealBaseUrl` = own origin
- [~] CSP directive set — baseline security headers shipped in `Caddyfile`; **CSP itself still TODO** (ADR-0021, pin against the running app)
- [~] Brevo behind the SMTP sender — `SmtpMailSender` now authenticates when credentials are set; Brevo host/port in prod config, login/key via env. Still: **account + SPF / DKIM / DMARC** on the domain
- [ ] Postgres container on an NVMe volume; **nightly `pg_dump` → Swiss Backup**; **restore tested**
- [~] EF Core migrations as an explicit release step — one-shot `migrator` service (`--migrate`)
- [ ] Privacy policy + imprint + account-deletion URLs published, reachable, DE/EN/FR
- [~] Secrets: `.env.example` template + git-ignored `.env`; **durable host/CI store still an open decision**
- [ ] Observability backend chosen and wired
- [ ] Deploy & smoke-test; single-VM recovery (rebuild + restore) documented

## Related

- [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) — provider & topology
- [ADR-0037](../adr/0037-transactional-email-provider.md) — transactional email
- [Mobile apps](mobile-apps.md) — the other deployment track
- [arc42 ch. 7](../arc42/07-deployment-view.md) — deployment view
