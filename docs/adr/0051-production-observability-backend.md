---
status: Accepted
date: 2026-08-25
deciders: [Adrian]
tags: [observability, telemetry, deployment, operations, privacy, docker, opentelemetry, grafana, prometheus, loki]
---

# ADR-0051 — Production observability backend

## Context and problem statement

[ADR-0012](0012-observability-and-telemetry.md) chose **OpenTelemetry through
`CircleTools.ServiceDefaults`** (traces, metrics, logs) for every server host, the **Aspire dashboard
locally**, and **opt-in, consent-gated, no-PII** client telemetry. It left one follow-up explicit:
*"choose a telemetry backend at the deployment phase."* Milestone 8 (deployment) is active and the
Compose stack now exists ([ADR-0036](0036-cloud-provider-and-hosting.md)), so the question is due:
**where do the server's logs and metrics go in production, and how does the maintainer look at them?**

The instrumentation is already provider-agnostic and OTLP-native. `ServiceDefaults` wires the OTel
logging, metrics and tracing pipelines unconditionally; the exporter is a small seam. So the decision
is a **backend + a viewing surface**, mostly configuration, not re-instrumentation.

The framing requirement: the maintainer wants a **persistent admin view of logs and metrics** — open a
page days later and see what happened — using the **Prometheus + Grafana** model already run at work.
That requirement is what settles this ADR, and it rules out an ephemeral view. The proportionality test
from [ADR-0036](0036-cloud-provider-and-hosting.md) / [ADR-0050](0050-secrets-management.md) still
applies (**~CHF 12/mo single VPS, seasonal, single-maintainer, privacy-first, Swiss**), but
"proportionate" includes **operator familiarity**: a stack the maintainer already operates is cheaper
to run than a novel one.

## Decision drivers

- **A persistent admin view of logs + metrics is a stated need** — not a nice-to-have; the chosen tool
  must retain data and be reachable like a normal admin panel.
- **Operator familiarity** — Prometheus + Grafana are already run at work; that lowers the true cost of
  running them here.
- **Privacy / data residency** — Swiss-first, privacy-sensitive product. Telemetry carries **no PII**
  ([ADR-0012](0012-observability-and-telemetry.md)), but keeping even operational data **on the box we
  control** (not a foreign SaaS) is the posture-consistent default.
- **Proportionate to one small VPS** — no heavyweight platform to babysit; the footprint must fit
  4 GB alongside Postgres + API + Caddy, with the documented resize-to-L as the escape hatch.
- **Least public surface** — only the view is exposed (behind auth); the stores are never published.
- **Minimal, contained code change** — honour the OTLP/OpenTelemetry seam already built.

## Considered options

1. **Self-hosted Loki + Prometheus + Grafana on the same VPS.** Grafana is the persistent admin view;
   Prometheus scrapes the API's metrics; Loki stores its logs; all on-box.
2. **On-box logs + on-demand Aspire dashboard** (the first draft of this ADR): structured stdout logs
   as the floor, and the standalone Aspire dashboard spun up over an SSH tunnel for a live look.
3. **Managed OTLP SaaS free tier** — Grafana Cloud (EU), Dash0, Uptrace, Honeycomb, Axiom: point the
   production OTLP endpoint at a hosted backend, zero storage on the box.
4. **A heavier self-hosted platform now** — full LGTM (add Mimir, Tempo) or SigNoz, with object
   storage, traces and alerting from day one.

## Decision outcome

**Chosen: Option 1 — self-hosted Loki + Prometheus + Grafana on the VPS.** It is the only option that
delivers a *persistent* admin view of logs and metrics **and** keeps the data on the Swiss box, using
tooling the maintainer already knows. Concretely (as built in [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)):

### Metrics → Prometheus scrapes the API's `/metrics`

`ServiceDefaults` gains `AddPrometheusExporter()` and maps a **`/metrics`** scraping endpoint (all
environments). Prometheus **pulls** it over the internal Docker network (`api:8080/metrics`) on a 30s
interval, retaining 30 days on a local volume. This is the classic pull model the maintainer runs at
work. Every existing meter — ASP.NET Core, HttpClient, .NET runtime, and the custom `CircleTools.Sync`
counter (`circletools_sync_collisions`) — is scraped with no per-metric work. The endpoint is
**internal-only**: the API has no published port, and Caddy **denies `/metrics` (and `/alive`) on the
public `api.circletools.ch` origin**.

### Logs → the API exports OTLP logs to Loki

`ServiceDefaults` adds an OTLP **log** exporter (HTTP/protobuf) to the logging pipeline, **gated on a
config value** (`Observability:LokiOtlpEndpoint`, set to `http://loki:3100/otlp` in prod). Loki 3.x
ingests OTLP natively and retains 30 days on a local volume. Local dev is untouched — the value is
unset there, so Aspire keeps using its dashboard. In addition, the Docker **`json-file` driver stays as
the always-on floor** (rotated `max-size`/`max-file`), so `docker compose logs` still works on the box
even when Grafana/Loki is down — the one time you most need logs.

### View → Grafana, public origin behind its own login

Grafana runs on-box with **datasources auto-provisioned** (Prometheus default + Loki). It is the **only**
observability service exposed: Caddy reverse-proxies **`grafana.circletools.ch`**, protected by
Grafana's admin login (`GRAFANA_ADMIN_PASSWORD`, sign-up disabled, no anonymous access). **Prometheus
and Loki have no site block at all** — unreachable from the internet, queried only by Grafana on the
internal network.

### Scope now

**Logs + metrics only.** Traces are **deferred** (no Tempo yet) and **alerting** is not configured yet —
both are clean additions in Grafana later (recorded below). Telemetry **never leaves the box**,
reinforcing [ADR-0012](0012-observability-and-telemetry.md)'s privacy posture.

### Why not the alternatives

- **On-demand Aspire dashboard (Option 2)** was this ADR's first draft. Its store is **in-memory only**
  (wiped on restart), and its login is a dev-oriented browser token — so it is a live *peek*, not the
  *persistent admin view* that is the actual requirement. It stays as the **local-dev** tool (via Aspire,
  [ADR-0023](0023-local-dev-infrastructure-in-apphost.md)), not the production answer.
- **Managed SaaS (Option 3)** is low-effort and free at this volume, but it puts operational telemetry
  (even non-PII) in a third party, usually outside Switzerland — against the product's posture. It is
  the **preferred upgrade** if the on-box footprint ever becomes a problem (pick an EU/Swiss region).
- **Heavier platform now (Option 4)** — Mimir/Tempo/object-storage or SigNoz/ClickHouse — is real weight
  to run, size and back up on a 4 GB box: disproportionate for logs+metrics on one seasonal service.
  Adding **Tempo** (traces) or **alerting** to *this* stack is the incremental path when they earn it.

## Consequences

- **Positive:** a real, **persistent** admin view — Grafana dashboards, PromQL/LogQL, 30-day retention,
  real login — using tooling the maintainer already operates. Telemetry stays **on the Swiss box**
  (strongest residency posture; no third party). Only the view is exposed, behind auth; the stores are
  never published. Metrics use the familiar Prometheus **pull** model.
- **Positive:** the app code change is small and contained to `ServiceDefaults` (a Prometheus exporter +
  a config-gated OTLP-logs exporter); dev is unaffected.
- **Negative / accepted:** **~3 always-on containers** (Loki, Prometheus, Grafana) to run and size. At
  this load they fit VPS Lite M comfortably (Postgres + API + Caddy leave ~3 GB; the stack sits well
  under it); sustained pressure is the documented **resize-to-L** trigger
  ([ADR-0036](0036-cloud-provider-and-hosting.md)).
- **Negative / accepted:** **new ops surface** — Prometheus/Loki data volumes (expendable, retention-
  bounded) and Grafana's own config/dashboards (worth backing up). A **new host-runtime secret**
  (`GRAFANA_ADMIN_PASSWORD`) joins the [ADR-0050](0050-secrets-management.md) inventory. A **new public
  origin** (`grafana.circletools.ch`) needs a DNS record + TLS cert.
- **Negative / accepted:** **no traces and no alerting yet**; **Postgres/Caddy logs are not in Loki**
  (only the API's OTLP logs are) — they remain in `docker logs`. All three are additive later.
- **Follow-ups:**
  - Provision the **`grafana.circletools.ch`** DNS record + Cloudflare (same HTTP-01 caveat as the other
    origins, see [`deploy/README.md`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)).
  - Generate `GRAFANA_ADMIN_PASSWORD` into the vault and derive `.env` from it
    ([ADR-0050](0050-secrets-management.md)); add its rotation line to the runbook.
  - **Back up** Grafana's dashboards/config (provisioning is in git; runtime-created dashboards are not).
  - Add starter **dashboards** (API RED metrics, runtime, sync collisions) and **alert rules** under
    `deploy/grafana/provisioning/`.
  - Optional: a **log shipper** (Grafana Alloy / Promtail) to also land Postgres/Caddy container logs in
    Loki; and **Tempo** for distributed traces — both when they earn their weight.
  - Deliver [ADR-0012](0012-observability-and-telemetry.md)'s other open follow-up — **log-scrubbing
    guidance** in the [security & privacy guidelines](../guidelines/security-privacy-guidelines.md) — now
    that logs are a queryable production store.
  - Flip the **"Observability backend chosen and wired"** checklist item and the observability
    sub-decision note in the deploy runbook to done **once this ADR is Accepted** and the stack is
    provisioned. This ADR **closes ADR-0012's "choose a backend at deployment"** follow-up.
- **New risks:** an **exposed Prometheus/Loki** would leak internals — mitigated by giving them no site
  block (internal-only) and denying `/metrics` publicly on the API origin. A **weak Grafana password**
  exposes all dashboards — hence a strong secret, sign-up disabled, no anonymous access. **Unrotated
  logs / unbounded TSDB** could fill the disk — mitigated by json-file rotation and Prometheus/Loki
  retention.

## Upgrade paths (trigger-gated)

- **Grafana Cloud / managed SaaS** — if the on-box footprint becomes a problem (memory pressure, or not
  wanting to run the stack), ship the same OTLP/scrape data to a hosted backend (EU/Swiss region). A
  config change, not a rewrite.
- **Tempo (traces)** — add a Tempo container + datasource and point the OTLP **traces** endpoint at it
  when request-level tracing is worth it.
- **Alerting** — Grafana alert rules (error rate / latency / down) once push-notification monitoring is
  wanted.
- **Mimir / object storage** — if metric cardinality or retention outgrows local Prometheus TSDB.

## Pros and cons of the options

### Option 1 — Self-hosted Loki + Prometheus + Grafana (chosen)
- 👍 Persistent admin view (dashboards, retention, real login); data stays on-box; familiar tooling; only the view is exposed.
- 👍 Metrics via the familiar Prometheus pull model; small, contained app change.
- 👎 ~3 always-on containers to run/size/back up; a new secret + a new public origin; no traces/alerting yet.

### Option 2 — On-box logs + on-demand Aspire dashboard
- 👍 Near-zero footprint; identical dev/prod UX; telemetry stays on-box.
- 👎 **In-memory, ephemeral** and dev-oriented auth — not a persistent admin view. **Kept as the local-dev tool only.**

### Option 3 — Managed OTLP SaaS free tier
- 👍 Persistence + alerting for CHF 0 at this volume; zero box footprint; one-line endpoint change.
- 👎 Operational telemetry leaves the box to a third party, usually outside Switzerland — against the posture. **Upgrade path if the footprint hurts (EU/Swiss region).**

### Option 4 — Heavier platform now (LGTM+Mimir/Tempo, or SigNoz)
- 👍 Traces + alerting + object storage out of the box.
- 👎 Several more services + a storage engine to run and back up on a 4 GB box — disproportionate for logs+metrics on one seasonal service. **Add Tempo/alerting incrementally instead.**

## Links

- Related ADRs: [ADR-0012](0012-observability-and-telemetry.md) (OTel + no-PII + client opt-in — raised
  this follow-up), [ADR-0036](0036-cloud-provider-and-hosting.md) (provider/topology; proportionality;
  VPS sizing + resize trigger), [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) (Aspire dashboard
  is dev infrastructure), [ADR-0050](0050-secrets-management.md) (the new `GRAFANA_ADMIN_PASSWORD` joins
  its inventory; same on-box, trigger-gated pattern), [ADR-0021](0021-web-auth-token-model.md) /
  [ADR-0049](0049-disable-wasm-fingerprinting-for-csp.md) (the CSP the API origin already carries)
- Guidelines: [security & privacy](../guidelines/security-privacy-guidelines.md) (no PII in
  telemetry/logs; log-scrubbing follow-up)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md),
  [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md),
  [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md)
- Runbooks: [`docs/deployment/web-api-db.md`](../deployment/web-api-db.md)
- Code: [`CircleTools.ServiceDefaults/Extensions.cs`](https://github.com/CircleTools-ch/circletools-suite/blob/main/src/Aspire/CircleTools.ServiceDefaults/Extensions.cs)
  (Prometheus exporter + OTLP-logs seam), deploy config under
  [`deploy/prometheus`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/prometheus),
  [`deploy/loki`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/loki),
  [`deploy/grafana`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/grafana)
- Roadmap: [milestone 8](../roadmap/roadmap.md)
