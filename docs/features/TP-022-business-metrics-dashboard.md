---
id: TP-022
feature: FEAT-022
status: Draft
related: [ADR-0051, ADR-0012, ADR-0020, NFR-004]
---

# TP-022 — Test plan for FEAT-022

## Scope & objectives

Verify that the Main service emits **correct, PII-free** business counts (accounts, groups, events, draws,
members) as OpenTelemetry metrics, that collection is off the scrape path and best-effort, and that the
numbers reach Grafana through the existing Prometheus pipeline (ADR-0051). The sharpest risk is **privacy
leakage** (a stray high-cardinality or PII-bearing tag — ADR-0012/0020), so the plan weights the
metric-hygiene check as heavily as the count-correctness checks.

## Test levels

- **Unit** — the draws derivation (distinct `(GroupId, EventId, DrawVersion)`) and snapshot read/write
  under concurrency (volatile fields; last-writer-wins is fine).
- **Integration** — `BusinessMetricsCollector` against a **real Postgres** (Testcontainers) seeded with
  known rows across all five schemas; assert the snapshot matches.
- **Architecture** — ServiceDefaults does not reference the app modules (meter name stays a literal);
  existing boundary fitness functions stay green.
- **Metric hygiene** — enumerate the emitted instrument names and tag keys; assert the closed allow-list.
- **UI/component** — n/a (no Blazor surface).
- **End-to-end / manual** — dev `https-observability` profile: create data in the app, observe `/metrics`
  and the Grafana panels.
- **Exploratory** — confirm the dashboard reads correctly (draws labelled "server-visible", two group
  kinds separated) and that an empty database shows zeros, not gaps/errors.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| Accounts counted | Seed N users (some `EmailConfirmed`), collect → `accounts{confirmed=true}`=k, `{false}`=N−k | Integration | ✅ |
| Groups counted by kind | Seed a circles rows + b shared groups → `groups{kind=circle}`=a, `{kind=shared}`=b | Integration | ✅ |
| Events counted | Seed e `group_events` → `events`=e | Integration | ✅ |
| Members counted | Seed m `circle_memberships` → `members`=m | Integration | ✅ |
| **Draws derived** | Seed assignments for 2 groups × 2 draw versions → `draws`=4 (distinct tuples), not the row count | Unit + Integration | ✅ |
| Empty DB | Collect against an empty schema → all gauges = 0, no exception | Integration | ✅ |
| Best-effort on failure | DB made unreachable mid-run → previous snapshot kept, error logged, host survives | Integration | ✅ |
| Off the scrape path | Gauge callback reads the snapshot only (no DbContext resolved in the callback) | Unit | ✅ |
| **No PII / cardinality** | Emitted tag keys ⊆ {`confirmed`,`kind`}; no id/email/name tag; instrument names match the spec | Metric hygiene | ✅ |
| Meter registered | `CircleTools.Business` is collected by the OTel meter provider (gauges observable) | Integration | ✅ |
| Prometheus shape | `/metrics` exposes `circletools_business_accounts` etc. with expected labels | Manual/E2E | ➖ |
| Dashboard reads | `circletools-business.json` panels render totals + growth; draws labelled partial | Exploratory | ➖ |

## Test data & environments

- **Integration:** Testcontainers Postgres 17 with all four migration sets applied (as the repository
  integration tests already do); a seeding helper inserts users, circles, shared groups, `group_events`,
  `circle_memberships`, and `assignments` with controlled `DrawVersion`s.
- **Manual:** AppHost `https-observability` launch profile (`Observability:DevStack=true`) — the same
  Loki/Prometheus/Grafana config as production via bind mounts; drive the Secret-Santa web app to create a
  group, add an event, run a draw, and register an account, then watch the panels.

## Localization & accessibility notes

- None. Operator/ops surface in English only (not product UI); no a11y obligations beyond Grafana's own.

## Exit criteria

- [ ] All count cases above pass against real Postgres, including the draws derivation and the empty-DB case
- [ ] Metric-hygiene test green: no tag outside the allow-list, no PII, instrument names as specified
- [ ] Collector proven best-effort (survives a DB blip) and off the scrape path
- [ ] Manual dev-stack walkthrough: `/metrics` exposes the series and Grafana panels move
- [ ] CI green
