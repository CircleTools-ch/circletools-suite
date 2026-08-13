---
status: Accepted
date: 2026-08-13
deciders: [Adrian]
tags: [deployment, hosting, infrastructure, swiss, privacy, cost]
---

# ADR-0036 — Cloud provider & hosting topology

## Context and problem statement

The Secret-Santa MVP is local-first and needs no server, but **v2 changed that**: M1–M4 store personal
data, circle membership and Secret-Santa assignments **server-side**
([ADR-0020](0020-server-side-assignment-privacy.md), [ADR-0022](0022-account-deletion-and-data-export.md)),
behind an ASP.NET API talking to PostgreSQL. None of it can *ship* until there is somewhere to run it.
[ADR-0023](0023-local-dev-infrastructure-in-apphost.md) deliberately bought time — the AppHost runs the
same containers locally behind provider-agnostic seams — but it explicitly did **not** close this
decision, and M1 cannot go live without it.

The shape to host is fixed by [ADR-0005](0005-web-hosting-render-model.md): a **standalone Blazor WASM
PWA** (static files), a **separate Web API**, and **managed or self-managed PostgreSQL** — three things
to place, not a coupled server-rendered app. The API is modest: account/session, circles, the `/sync`
delta exchange, and the assignment projection. Real load is **strongly seasonal** — a Secret-Santa suite
is near-idle for eleven months and spikes in November–December — so an always-cheap, always-on small host
beats anything whose value is elastic scale.

Two properties raise the stakes beyond "pick a box":

- **Swiss data residency.** The suite is Swiss-first and now stores PII and assignments; keeping data in
  Switzerland is the posture [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
  and [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md) point at.
- **The chosen origin is permanent in two places.** The deployed domain fixes the **passkey
  relying-party ID**, which does not migrate ([ADR-0023](0023-local-dev-infrastructure-in-apphost.md),
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md)), and it fixes the **CSP directive set**
  and the Secret-Santa `RevealBaseUrl`. So this decision is coupled to the still-open
  [brand & domain](../process/naming.md) gate — the *provider* can be chosen now, but the *origin* can
  only be finalised once the domain lands.

**Which provider and topology host the Web, API and database — cheaply, in Switzerland, for a single
maintainer?**

## Decision drivers

- **Swiss data residency** for PII and assignments (NFR-004, NFR-008).
- **Cost proportional to a free hobby suite** — every recurring franc is spent by one person for
  friends and family, with no revenue behind it.
- **Operational simplicity.** One maintainer, no ops team. The stack must be diagnosable and
  restartable by one person on a bad evening.
- **Seasonal, low load.** Idle most of the year; a modest Nov–Dec peak. Elastic autoscaling and
  scale-to-zero buy nothing here and cost complexity.
- **Reuse the existing relationship.** Domains are already bought at **Infomaniak** (Swiss, Geneva);
  consolidating domain, host, backup and role mailbox under one Swiss provider is one less account,
  one less bill, one residency story.
- **Backups and restore** must be offsite from the compute and actually tested.
- **Don't over-engineer.** Kubernetes is disproportionate to a single small API + Postgres.
- **Keep the door to managed pieces open** without rebuilding — ADR-0023's seams already allow it.

## Considered options

1. **Single Infomaniak VPS + Docker Compose**, self-managed Postgres container, nightly dump to
   Infomaniak Swiss Backup, Cloudflare Free in front, static WASM served by the reverse proxy.
2. **Infomaniak Jelastic Cloud (PaaS)** — containers with auto-scaling, no VM to administer.
3. **Infomaniak Public Cloud (OpenStack) + managed database** (Infomaniak DB service, or
   **Exoscale DBaaS** for managed Postgres in a Swiss zone).
4. **Managed Kubernetes** (Infomaniak or Exoscale SKS).
5. **A hyperscaler** — e.g. Azure Container Apps, the Aspire "paved path" (`aspire publish`).

## Decision outcome

**Chosen: option 1 — a single Infomaniak Cloud VPS running Docker Compose, with self-managed Postgres,
offsite dumps to Swiss Backup, and Cloudflare Free at the edge.**

### Topology

```
Cloudflare (Free: DNS, TLS, CDN, DDoS, basic WAF)
        │
        ▼
Infomaniak VPS (Swiss, ~2 vCPU / 4 GB / 60 GB NVMe)
  ├── Caddy (or Traefik)  — reverse proxy, Let's Encrypt TLS, serves the static WASM PWA
  ├── api        (ASP.NET container)   ──►  postgres (container, NVMe volume)
  └── nightly pg_dump  ──►  Infomaniak Swiss Backup (S3/Swift, offsite)
```

- **One VM, Docker Compose.** The API container, the Postgres container and the reverse proxy live on
  one small Infomaniak VPS. Aspire's model maps onto this directly — `aspire publish` can emit a Compose
  manifest, and the ADR-0023 seams mean local `AppHost` → one Compose file is a bridge, not a rewrite.
- **Self-managed Postgres, offsite dumps.** Postgres runs as a container on an NVMe volume; a **nightly
  `pg_dump` is pushed to Infomaniak Swiss Backup**, which is physically separate from the compute. A
  **restore is tested**, not assumed. Managed Postgres (option 3) is the documented upgrade path if
  backup discipline becomes a chore — the app is provider-agnostic behind EF Core, so the switch is a
  connection string.
- **Static WASM at the edge.** The PWA is static files; the reverse proxy serves them with cache-busting
  per release (the service-worker discipline ADR-0005 requires), and Cloudflare caches them.
- **Cloudflare Free** provides DNS, TLS, a CDN and DDoS/WAF at no cost. Accepted trade-off: proxied
  **public app traffic** transits Cloudflare (a US company) — it terminates TLS on already-public
  request/response traffic; it does **not** store the database or PII. For a free family app this is
  proportionate; the residency line that matters (data at rest) stays in Switzerland. Dropping Cloudflare
  and relying on Caddy TLS + the provider's DDoS is a one-setting reversal if the residency posture ever
  hardens.
- **One production environment + local Aspire dev.** No staging until pain justifies it.

### Indicative cost (CHF, ex-VAT; Infomaniak quotes CHF/EUR near parity — treat as ±10 %)

| Piece | Provider | Spec | Cost |
|---|---|---|---|
| Domain `.ch` | Infomaniak | registration + renewal | ~CHF 11 / yr |
| Compute | Infomaniak VPS Lite | 2 vCPU / 4 GB / 60 GB NVMe, unlimited traffic | ~CHF 8–9 / mo |
| Offsite backup | Infomaniak Swiss Backup | 200 GB | CHF 2.40 / mo (1 TB = 4.18) |
| CDN / TLS / DDoS / WAF | Cloudflare Free | edge proxy | CHF 0 |
| Role mailbox `dev@<brand>` | Infomaniak Mail | 1 custom-domain mailbox | ~CHF 0–1.50 / mo |

**Recurring web/cloud ≈ CHF 12–14/mo (~CHF 150/yr).** Transactional email is decided separately in
[ADR-0037](0037-transactional-email-provider.md) (Brevo free tier, CHF 0 at this volume). The full
shipping budget including the store fees and the iOS build Mac is in the
[mobile runbook](../deployment/mobile-apps.md); the largest single cost of the whole effort is the Mac
([ADR-0031](0031-ios-build-host.md)), not any of the above.

## Consequences

- **Positive:** M1 gains a live target; data at rest stays in Switzerland; the recurring cost is
  roughly a coffee a week. One provider (Infomaniak) carries domain, host, backup and the role mailbox —
  one relationship, one residency story, one bill.
- **Positive:** the topology is diagnosable by one person — a VM, three containers, a Compose file — and
  the Aspire seams mean the deployed shape mirrors local dev.
- **Positive:** the eventual **CI/CD** pipeline (still open) can deploy by SSH-ing the VM and running
  `compose pull && up`, and the [self-hosted Mac](0031-ios-build-host.md) can host the macOS runner leg.
- **Negative / accepted:** a **single VM is a single point of failure**; recovery is rebuild-from-image
  plus restore-from-dump, not automatic failover. Proportionate for a free seasonal app; the mitigation
  is infra-as-code (the Compose file in the repo) and a tested restore.
- **Negative / accepted:** self-managed Postgres means backups, patching and disk headroom are the
  maintainer's job. The escape hatch (managed Postgres) is one connection string away.
- **Negative / accepted:** Cloudflare terminates TLS on public traffic outside Switzerland (data at rest
  does not leave). Reversible.
- **Follow-ups:**
  - **Register the suite domain at Infomaniak** once [naming](../process/naming.md) lands, and **open a
    project-owned Infomaniak account** for the host/backup (not a personal login), mirroring the
    dedicated-account reasoning of [ADR-0030](0030-mobile-store-publisher-identity.md).
  - Set the **passkey RP-ID**, the **CSP directive set**, and the MAUI `RevealBaseUrl` to the final
    origin — none of these can be finalised before the domain (naming risk **R8**).
  - Publish the **privacy policy, imprint and account-deletion URLs** on the domain (DE/EN/FR) — both
    a legal duty (NFR-004) and a store prerequisite ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
  - Decide the **production observability backend** — the Aspire dashboard ([ADR-0012](0012-observability-and-telemetry.md))
    is dev-only; pick a self-hosted or free managed OTel sink, minding data egress.
  - Decide **secrets management** for CI and the host (GitHub Actions secrets + provider store; DB
    credentials, signing assets and the Brevo API key never in the repo).
  - Feed the **CI/CD** ADR (still open) and record the **single-VM recovery** risk in
    [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).
  - Fill the [web/API/database runbook](../deployment/web-api-db.md) with the concrete provisioning steps
    (started with this ADR's topology and cost table).
- **New risks:** the suite's uptime rests on one VM and one provider account; a lost provider login or a
  dead disk is an outage whose only remedy is rebuild-and-restore. Guarded by offsite dumps, a tested
  restore, and infra-as-code.

## Pros and cons of the options

### 1. Single Infomaniak VPS + Docker Compose (chosen)
- 👍 Cheapest always-on Swiss option (~CHF 12–14/mo all-in); one provider for domain+host+backup+mail.
- 👍 Simple enough for one maintainer; mirrors the Aspire dev topology; trivial CI deploy target.
- 👍 Managed Postgres remains one connection-string away if self-managing gets old.
- 👎 Single point of failure; recovery is manual rebuild + restore.
- 👎 Postgres operations are the maintainer's responsibility.

### 2. Infomaniak Jelastic PaaS
- 👍 No VM to administer; auto-scales; Swiss; from ~CHF 6.31/mo for a tiny container.
- 👍 CHF 300 trial credit to size it.
- 👎 For steady tiny load, pay-per-cloudlet is dearer and less predictable than a fixed small VPS.
- 👎 Another deployment model to learn for no benefit the seasonal load can use.

### 3. Public Cloud (OpenStack) + managed DB (Infomaniak / Exoscale DBaaS)
- 👍 Managed Postgres removes the backup/patching chore (automated backups, PITR).
- 👍 The right call once the DB matters more than the budget.
- 👎 +~CHF 20–30/mo for managed Postgres; more moving parts (OpenStack) than one small service needs now.

### 4. Managed Kubernetes (Infomaniak / Exoscale SKS)
- 👍 Industry-standard; free control plane on some tiers.
- 👎 Wildly disproportionate operational surface for one API + one database.

### 5. Hyperscaler (Azure Container Apps, Aspire paved path)
- 👍 `aspire publish` targets it directly; scale-to-zero; least infra to hand-build.
- 👎 Swiss data residency is awkward and pricier; couples a hobby suite to hyperscaler billing and lock-in.
- 👎 Overkill for the load; against the cost driver.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md) (what is hosted — static WASM + API),
  [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) (provider-agnostic dev seams this builds on),
  [ADR-0007](0007-persistence-approach.md) (EF Core → Postgres, keeps the DB swappable),
  [ADR-0012](0012-observability-and-telemetry.md) (telemetry; prod backend is a follow-up),
  [ADR-0037](0037-transactional-email-provider.md) (email, decided alongside),
  [ADR-0030](0030-mobile-store-publisher-identity.md) (dedicated project-owned accounts pattern)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md),
  [11 Risks and technical debt](../arc42/11-risks-and-technical-debt.md)
- Requirements: [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md)
- Process: [naming](../process/naming.md) (domain gate — prerequisite for the final origin)
- Runbook: [Deployment — Web / API / Database](../deployment/web-api-db.md)
