---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, aspire, blazor, wasm, cors]
---

# ADR-0013 — Standalone WASM ↔ API wiring under Aspire

## Context and problem statement

Per [ADR-0005](0005-web-hosting-render-model.md) the web client is a **standalone Blazor WebAssembly
PWA** talking to a **separate Web API**. .NET Aspire's **service discovery is server-side**: a
WebAssembly app runs in the browser and cannot consume the discovery configuration Aspire injects into
server projects (dotnet/aspire #8486, #7524). So the WASM client must learn its API base URL another
way, and cross-origin calls need CORS. How do we wire the client to the API — in local dev and later?

## Decision drivers

- Keep the very first code **dependency-light** and easy to reason about.
- Don't pre-commit the deferred **web auth token model** (bearer vs BFF/cookie).
- Must work under `dotnet run` on the Aspire AppHost.

## Considered options

1. **Manual config + CORS** — WASM reads `ApiBaseUrl` from `wwwroot/appsettings.json`; the API enables a CORS policy for the WASM origin; fixed dev ports make it deterministic.
2. **Aspire4Wasm** library — flows Aspire service-discovery info into the WASM client and helps configure CORS.
3. **YARP BFF gateway** — a reverse proxy puts client + API on one origin (no CORS) and enables cookie/BFF auth.

## Decision outcome

**Chosen: manual config + CORS** for now. The WASM `wwwroot/appsettings.json` carries `ApiBaseUrl`;
the API reads `Cors:AllowedOrigins` and allows the client origin; the AppHost sets these values and the
projects use fixed dev ports. Zero extra dependencies, and it stays neutral w.r.t. the future auth model
(works fine with bearer tokens in the WASM).

## Consequences

- **Positive:** Simplest possible wiring; no third-party dependency; transparent; doesn't force a BFF.
- **Negative:** Config is manual (a base URL + an allowed origin) rather than auto-discovered; CORS must
  be maintained per environment.
- **Follow-ups:** Revisit when the **web auth token model** is decided — if we choose a cookie/BFF
  approach, migrate to a **YARP BFF** (same-origin, no CORS) and supersede this ADR. `Aspire4Wasm` remains
  an option if manual config becomes tedious across environments.

## Pros and cons of the options

### 1. Manual config + CORS
- 👍 No deps, transparent, auth-neutral.
- 👎 Manual base URL + CORS upkeep.

### 2. Aspire4Wasm
- 👍 Purpose-built; flows discovery + CORS.
- 👎 Third-party dependency in the very first code.

### 3. YARP BFF
- 👍 Same-origin (no CORS); enables secure cookie/BFF auth.
- 👎 More infrastructure now; pre-commits the deferred auth decision toward BFF.

## Links

- Related: [ADR-0005](0005-web-hosting-render-model.md), [ADR-0009](0009-authentication-and-passkeys.md) (web token model still open)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md)
