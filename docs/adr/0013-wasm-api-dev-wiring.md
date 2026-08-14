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

## Amendment (2026-08-13) — the wiring now covers both WASM clients

When this ADR was written the suite had one standalone WASM client (the Main shell). Secret-Santa
was a purely offline, account-free PWA and was deliberately **not** orchestrated by the AppHost — it
was started on its own. Secret-Santa has since grown the shared account/session block
([ADR-0034](0034-shared-account-session-ui-block.md)), sync ([ADR-0033](0033-which-aggregates-sync.md))
and the circle/linking reads, so it now reaches the same Main API as the Main shell and needs the
same two things this ADR provides: an `ApiBaseUrl` and a CORS allow-entry.

Nothing about the **decision** changes — it is still manual config + CORS. It now simply applies to
**both** clients:

- The AppHost adds `secretsanta-web` alongside `main-web`, each `WithReference(api)`.
- The API's `Cors:AllowedOrigins` gets **one indexed entry per client** (`__0` = `main-web`,
  `__1` = `secretsanta-web`).
- Each client keeps its own `wwwroot/appsettings.json` `ApiBaseUrl` (both point at the API's fixed
  dev https port). The account-free visitor never contacts the API, so its value is inert for them
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).

The MAUI head stays out of the AppHost: it is a device app, not an orchestrated service, and supplies
its own platform seams ([ADR-0028](0028-browser-platform-adapters-block.md)).

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

- Related: [ADR-0005](0005-web-hosting-render-model.md), [ADR-0009](0009-authentication-and-passkeys.md); the web token model this ADR avoided pre-committing is now settled in [ADR-0021](0021-web-auth-token-model.md) (bearer in WASM, no BFF)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md)
