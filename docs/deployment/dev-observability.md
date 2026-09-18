# Local observability (validate before deploy)

> Runs the **same** Loki + Prometheus + Grafana stack decided in
> [ADR-0051](../adr/0051-production-observability-backend.md) on your machine, under the .NET Aspire
> AppHost ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)), so you can see the
> production dashboards, datasources and log view **before** shipping to the VM. It reuses the exact
> [`deploy/grafana`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/grafana) and
> [`deploy/loki`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/loki) config
> unchanged; only the Prometheus scrape target differs (explained below).

A green test run does not prove a dashboard reads correctly or that a log actually lands in Loki — you
have to look. This profile lets you do that locally instead of discovering it on the box after a deploy.

## Run it

```bash
dotnet run --project src/Aspire/CircleTools.AppHost --launch-profile https-observability
```

The `https-observability` launch profile is the normal `https` profile plus `Observability:DevStack=true`.
Day-to-day dev is unchanged: a plain `dotnet run` (default profile) starts **without** the observability
containers, so it stays lean and fast. The stack is **opt-in** precisely because it adds three
containers and startup time you don't want on every quick UI run.

Once up, open **Grafana at <http://localhost:3000>** — anonymous admin is enabled **for dev only** (the
production Grafana keeps its login, [ADR-0051](../adr/0051-production-observability-backend.md)). Then:

- **Explore → Loki**, query `{service_name="main-api"}` — the filter/drill-down log view (the
  work-SEQ equivalent), with the level-broken-down volume histogram.
- **Dashboards → CircleTools → "CircleTools — Service & Host"** — the provisioned production dashboard,
  populated live by the dev API.

## How it maps to production

| Piece | Production ([`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)) | Local (Aspire) | Same config? |
|---|---|---|---|
| **Grafana provisioning** (datasources + dashboard) | bind-mounted from `deploy/grafana/provisioning` | bind-mounted from the **same** files | ✅ byte-identical |
| **Loki** | `deploy/loki/loki-config.yaml` | the **same** file | ✅ byte-identical |
| **Datasource DNS** | Grafana → `prometheus:9090` / `loki:3100` | resolved by Aspire resource name — **same hostnames** | ✅ no edit |
| **Metrics scrape** | Prometheus pulls `api:8080/metrics` on the compose network | Prometheus (container) pulls `host.docker.internal:5180/metrics` — the API runs as a **host process**, not a container | ⚠️ dev-only `prometheus.dev.yml` |
| **Logs** | API → `http://loki:3100/otlp/v1/logs` | API → `http://localhost:3100/...` (the published Loki port) | same code path, dev endpoint |

The **only** genuine divergence is the Prometheus scrape topology, and it is unavoidable: the API is a
debuggable host process locally, not a container, so Prometheus reaches it via `host.docker.internal`
rather than a compose hostname. That lives in a separate
[`deploy/prometheus/prometheus.dev.yml`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy/prometheus)
that keeps the same `service="circletools-api"` label, so every dashboard query resolves identically to
production. Everything Grafana renders is what will render on the VM.

## Scope of the local mirror

- **Lean by design.** Only the three stores run. **node_exporter / cAdvisor are not included**, so the
  dashboard's **Host CPU / RAM / Disk / uptime** tiles read *No data* locally — expected; those are
  host-machine metrics that only make sense on the VM.
- **Logs go to Loki *instead of* the Aspire dashboard** in this profile. OpenTelemetry forbids a
  signal-specific OTLP log exporter (→ Loki) and the cross-cutting `UseOtlpExporter` (→ Aspire
  dashboard) on one service collection, so `ServiceDefaults` picks Loki when `Observability:LokiOtlpEndpoint`
  is set. Use **Grafana** for logs here; the Aspire dashboard still shows resource state and console logs.
- **No traces locally** either (same reason; and there is no Tempo — matching production's deferred-traces
  scope in ADR-0051).

## Gotchas found while wiring this up

- **Child projects are pinned to their `https` launch profile** in `AppHost.cs`. Aspire otherwise
  selects each project's launch profile by **matching the AppHost profile name**; a custom AppHost
  profile name (like `https-observability`) matched nothing and fell back to the projects' first (`http`)
  profile — which left the web apps with no `https` endpoint and crashed the API when it tried to resolve
  their CORS origins. Pinning removes that dependency on the profile name.
- **`OTEL_SERVICE_NAME`** must be set for the API in production
  ([`deploy/compose.yaml`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)); without
  it the OTel SDK falls back to `unknown_service:dotnet` and Loki's `service_name` label is useless. In
  dev, Aspire supplies it automatically.
- **Stopping the stack:** `Ctrl+C` the `dotnet run`. If an AppHost run is killed abruptly, Aspire's
  child processes (the API, the DCP proxies) and the observability containers can be **orphaned** and
  hold their ports; clear them before the next run (`docker rm -f` the loki/prometheus/grafana
  containers, and stop stray `dotnet` / `dcp` processes).

## See also

- [ADR-0051 — Production observability backend](../adr/0051-production-observability-backend.md) (the
  decision this mirrors)
- [arc42 ch. 7 — Deployment view](../arc42/07-deployment-view.md),
  [ch. 8 — Crosscutting concepts](../arc42/08-crosscutting-concepts.md) (observability)
- Code:
  [`src/Aspire/CircleTools.AppHost/AppHost.cs`](https://github.com/CircleTools-ch/circletools-suite/blob/main/src/Aspire/CircleTools.AppHost/AppHost.cs)
  (the opt-in container block),
  [`CircleTools.ServiceDefaults/Extensions.cs`](https://github.com/CircleTools-ch/circletools-suite/blob/main/src/Aspire/CircleTools.ServiceDefaults/Extensions.cs)
  (the OTLP exporter seam)
