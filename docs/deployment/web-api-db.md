# Deployment — Web / API / Database

How the server side of the suite is hosted: the Blazor WASM PWA, the Web API, and PostgreSQL. The
provider and topology are decided in [ADR-0036](../adr/0036-cloud-provider-and-hosting.md); the
transactional email sender in [ADR-0037](../adr/0037-transactional-email-provider.md). The other
deployment track — the native heads — is [mobile apps](mobile-apps.md).

> **Status:** decided, not yet executed. This runbook is filled in at
> [milestone 8](../roadmap/roadmap.md). Everything here has a hard **prerequisite on the
> [brand & domain](../process/naming.md)** — the final origin fixes the passkey relying-party ID, the
> CSP directive set, the mail *from*-address and the Secret-Santa `RevealBaseUrl`, none of which can be
> set on a placeholder domain once a real user or passkey exists.

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
  gate (build/test/docs + MAUI heads) is built; the **CD** step to this VM (SSH → `compose pull && up`,
  with EF Core migrations as an explicit release step) is built here at milestone 8 once the VM exists.
- **Secrets management** — DB credentials, the Brevo API key, signing assets; host/CI secret store,
  never in the repo.
- **Production observability backend** — the Aspire dashboard
  ([ADR-0012](../adr/0012-observability-and-telemetry.md)) is dev-only.

## Checklist (to build out at milestone 8)

- [ ] Brand & domain final; domain registered at Infomaniak
- [ ] Project-owned Infomaniak account (not a personal login) for host + backup
- [ ] VPS provisioned; Docker + Compose; reverse proxy with Let's Encrypt TLS
- [ ] Cloudflare in front (DNS, proxy, TLS); WAF baseline
- [ ] Passkey RP-ID, CSP directive set, and MAUI `RevealBaseUrl` pointed at the final origin
- [ ] Brevo account + SPF / DKIM / DMARC records on the domain; from-address on the suite domain
- [ ] Postgres container on an NVMe volume; **nightly `pg_dump` → Swiss Backup**; **restore tested**
- [ ] EF Core migrations run as an explicit release step
- [ ] Privacy policy + imprint + account-deletion URLs published, reachable, DE/EN/FR
- [ ] Secrets in the host/CI store, none in the repo
- [ ] Observability backend chosen and wired
- [ ] Deploy & smoke-test; single-VM recovery (rebuild + restore) documented

## Related

- [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) — provider & topology
- [ADR-0037](../adr/0037-transactional-email-provider.md) — transactional email
- [Mobile apps](mobile-apps.md) — the other deployment track
- [arc42 ch. 7](../arc42/07-deployment-view.md) — deployment view
