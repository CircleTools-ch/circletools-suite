---
status: Accepted
date: 2026-09-16
deciders: [Adrian]
tags: [deployment, hosting, dns, tls, privacy, swiss, cost]
---

# ADR-0063 — Drop Cloudflare: Infomaniak DNS + Caddy TLS (HTTP-01)

## Context and problem statement

[ADR-0036](0036-cloud-provider-and-hosting.md) chose an Infomaniak VPS with **Cloudflare Free at the
edge** for DNS, TLS, CDN, DDoS and a basic WAF. It also recorded that Cloudflare terminates TLS on
public traffic outside Switzerland and that **dropping it is a one-setting reversal** if the residency
posture ever hardens.

At go-live provisioning (milestone 8) the concrete state is: `circletools.ch` is registered at Infomaniak
and **all DNS is managed at Infomaniak today; nothing is configured at Cloudflare**. Putting Cloudflare in
front would mean delegating the zone (change nameservers Infomaniak → Cloudflare) and managing every
record — including the SPF/DKIM/DMARC for Brevo mail — at Cloudflare. The Cloudflare proxy is also the
sole reason the deployment needed the ACME **DNS-01** challenge (HTTP-01 is intercepted by the orange-cloud
proxy), which added a custom Caddy build (`caddy-dns/cloudflare`) and a scoped API token.

Weighing that against the realistic benefit for a **free, seasonal, single-maintainer, Swiss-first family
app**: the CDN and DDoS/WAF wins are modest at this traffic profile, while the cost is a US company in the
request path (seeing technical connection data on public traffic) and a second provider to manage.

**Should Cloudflare be introduced at the edge, or should the suite stay all-Infomaniak with Caddy handling
TLS directly?**

## Decision drivers

- **Swiss-first residency / privacy** — keep the whole request path in Switzerland where practical
  ([NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)); a cleaner, simpler
  privacy story for the public legal pages.
- **Operational simplicity** — one provider (Infomaniak: domain + DNS + host + backup + mail), one
  dashboard, one relationship (the consolidation ADR-0036 already valued).
- **Proportionality** — don't run infrastructure whose benefit the actual load can't use.
- **Least moving parts in TLS** — HTTP-01 "just works" with no proxy in front; DNS-01 needs a plugin build
  and a zone-scoped token.
- **Reversibility** — the choice must stay easy to revisit if traffic or the threat model grows.

## Considered options

1. **Cloudflare in front** (ADR-0036 as written): delegate the zone to Cloudflare; gain CDN/DDoS/WAF and
   origin-IP hiding; use Caddy DNS-01 via `caddy-dns/cloudflare` + a scoped token.
2. **All-Infomaniak, Caddy TLS via HTTP-01** (this ADR): DNS stays authoritative at Infomaniak, A records
   point straight at the VPS, Caddy obtains Let's Encrypt certs with the default HTTP-01 challenge. No
   proxy, no plugin, no token.

## Decision outcome

**Chosen: option 2 — drop Cloudflare. DNS stays at Infomaniak; Caddy terminates TLS on the VPS using the
default HTTP-01 challenge.** This supersedes the Cloudflare parts of [ADR-0036](0036-cloud-provider-and-hosting.md)
(edge provider, DNS-01 challenge, the CDN/DDoS/WAF row and its topology diagram); everything else in
ADR-0036 (Infomaniak VPS, self-managed Postgres, Swiss Backup, Caddy reverse proxy, single-VM recovery)
stands unchanged.

Concretely:

- **DNS** is managed at Infomaniak; A records for `circletools.ch`, `secretsanta`, `api` and
  `grafana` point directly at the VPS public IP.
- **TLS** is Caddy's automatic Let's Encrypt over **HTTP-01** — reachable because nothing intercepts
  `:80`. No `caddy-dns/cloudflare` build, no `CLOUDFLARE_API_TOKEN`.
- **The residency story simplifies:** data at rest and TLS termination are both in Switzerland; the only
  data that leaves the country is the handful of transactional emails through Brevo (EU,
  [ADR-0037](0037-transactional-email-provider.md)). The public **privacy policy and imprint** are updated
  to remove Cloudflare accordingly.

### What is given up (accepted)

No global CDN (every asset is served from the single Geneva box — fine for a Swiss-first audience, and the
service worker already caches client-side per [ADR-0048](0048-web-app-update-delivery.md)); no
Cloudflare DDoS scrubbing or managed WAF; the VPS public IP is exposed. These are proportionate for a free
seasonal app, and adopting Cloudflare later is still the one-setting reversal ADR-0036 described.

## Consequences

- **Positive:** the entire request path stays in Switzerland; the privacy posture and its public wording
  get simpler and stronger. One provider, one DNS control plane, one bill. TLS drops to the stock Caddy
  image + default HTTP-01 — the custom plugin build and the scoped token are removed.
- **Negative / accepted:** no edge CDN/DDoS/WAF and an exposed origin IP; mitigation is the VM's own
  hardening ([server setup](../deployment/server-setup.md)) and Infomaniak's baseline network protection.
  A single VM remains a single point of failure (unchanged from ADR-0036; guarded by offsite backups and a
  tested restore, [ADR-0036](0036-cloud-provider-and-hosting.md) / [backups](../deployment/backups.md)).
- **Follow-ups:**
  - Reverted the scaffold: stock `caddy:2-alpine` in `deploy/web.Dockerfile`, HTTP-01 in `deploy/Caddyfile`,
    `CLOUDFLARE_API_TOKEN` removed from `deploy/compose.yaml` and `deploy/.env.example`.
  - Updated the public **privacy policy** (`Legal_Privacy_ProcessorsBody`) and **imprint**
    (`Legal_Imprint_HostingBody`) strings in all locales (DE/EN/FR) to drop Cloudflare.
  - SPF/DKIM/DMARC for Brevo (GL-3) are created as Infomaniak DNS records, not Cloudflare.
- **New risks:** a volumetric DDoS or a targeted origin attack has no edge to absorb it; revisit
  Cloudflare (or Infomaniak's own protection tiers) if that threat becomes real.

## Pros and cons of the options

### Option 1 — Cloudflare in front
- 👍 Global CDN, free unmetered DDoS mitigation, basic managed WAF, origin-IP hiding, HTTP/3.
- 👎 A US company terminates TLS on public traffic (sees technical connection data outside CH/EU); a second
  provider and DNS control plane to manage; needs Caddy DNS-01 (plugin build + scoped token).

### Option 2 — All-Infomaniak + Caddy HTTP-01 (chosen)
- 👍 Whole path in Switzerland; simplest privacy story; one provider; stock Caddy + default HTTP-01.
- 👎 No CDN/DDoS/WAF; exposed origin IP; all delivery served from the single Geneva VPS.

## Links

- Supersedes (in part): [ADR-0036](0036-cloud-provider-and-hosting.md) — cloud provider & hosting (edge
  provider, DNS-01, CDN/DDoS/WAF); the VPS/Postgres/backup/recovery decisions there remain in force.
- Related ADRs: [ADR-0037](0037-transactional-email-provider.md) (Brevo, EU — the one remaining
  cross-border processor), [ADR-0048](0048-web-app-update-delivery.md) (service-worker caching, partial
  CDN substitute), [ADR-0050](0050-secrets-management.md) (removes a runtime secret),
  [ADR-0051](0051-production-observability-backend.md) (Grafana origin — same HTTP-01 path now)
- Requirements: [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) (data protection & residency)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md)
- Runbooks: [web / API / database](../deployment/web-api-db.md), [server setup](../deployment/server-setup.md),
  [`deploy/README.md`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
- Roadmap: [milestone 8](../roadmap/roadmap.md)
