---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, observability, telemetry, privacy]
---

# ADR-0012 — Observability & client telemetry

## Context and problem statement

We want good operational visibility on the server, but the product is privacy-sensitive (family data,
Swiss-first) and much of it runs on user devices. How do we handle observability without compromising
privacy?

## Decision drivers

- Strong server-side diagnostics for the API and background work.
- Privacy by design; families should not be silently tracked.
- Consistent, low-effort instrumentation.

## Considered options

1. **OpenTelemetry via `ServiceDefaults` server-side; client telemetry opt-in/consent-gated**
2. Full telemetry everywhere (server + client) by default
3. Minimal/no telemetry

## Decision outcome

**Chosen: OpenTelemetry through `Suite.ServiceDefaults`** for all server hosts (traces, metrics, logs;
Aspire dashboard locally), and **client-side telemetry that is opt-in and consent-gated**. **No PII** is
placed in traces, metrics, or logs anywhere.

## Consequences

- **Positive:** Rich server observability with almost no per-service effort; respects user privacy on
  devices; aligns with Swiss/GDPR expectations.
- **Negative:** Less visibility into client-side issues unless users opt in (acceptable trade-off; can
  supplement with explicit, anonymous, opt-in crash reporting later).
- **Follow-ups:** Define the consent UX and exactly what (if anything) the client may report; choose a
  telemetry backend at the deployment phase; add log-scrubbing guidance to the guidelines.

## Pros and cons of the options

### 1. Server OTel + opt-in client
- 👍 Great server insight; privacy-respecting; low effort.
- 👎 Limited default client visibility.

### 2. Full telemetry by default
- 👍 Maximum insight.
- 👎 Privacy risk; conflicts with the product's values and Swiss/GDPR posture.

### 3. Minimal/none
- 👍 Simplest; most private.
- 👎 Hard to diagnose production issues.

## Links

- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [security-privacy-guidelines.md](../guidelines/security-privacy-guidelines.md)
