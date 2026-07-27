---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, web, blazor, offline]
---

# ADR-0005 — Web hosting & render model

## Context and problem statement

The web target must have **feature parity with the native apps** and satisfy the hard **offline-first**
requirement (Q1): the app works fully with no account and no connection. .NET 10 offers several Blazor
hosting models. Which do we commit to for the web client?

## Decision drivers

- Offline-first is non-negotiable; the web app must work without a live server connection.
- Maximize code sharing with the native **MAUI Blazor Hybrid** apps.
- Keep the mental model simple and the client uniformly a "pure client of the API".
- First-class PWA/installability on desktop, tablet, and phone browsers.

## Considered options

1. **Standalone Blazor WebAssembly PWA** + separate Web API
2. **Blazor Web App** (unified) with `InteractiveWebAssembly` global render mode + manual PWA
3. **Blazor Web App** with `Auto` (Server → WebAssembly)

## Decision outcome

**Chosen: a standalone Blazor WebAssembly PWA client + a separate Web API.**

Both the web client and the MAUI Blazor Hybrid apps are then **pure clients** that render the same
Razor Class Libraries and talk to the API — a single, consistent architecture. The standalone WASM
template provides first-class PWA/offline support (service worker, installable, cached runtime).

## Consequences

- **Positive:** True offline/PWA out of the box; maximal reuse with native (identical client model);
  the server is a clean API with no UI-rendering responsibilities; simplest offline reasoning.
- **Negative:** No server-side rendering → weaker first-load performance and no SEO (acceptable — this
  is an authenticated/utility app, not content marketing). Larger initial download (mitigated by
  caching + trimming).
- **Follow-ups:** ~~Web-client **offline storage** mechanism~~ — decided in
  [ADR-0015](0015-mvp-client-offline-storage.md) (`localStorage`) and confirmed under sync by
  [ADR-0016](0016-sync-and-conflict-resolution.md). The **web auth token model** that this ADR's
  serverless choice constrains is settled in [ADR-0021](0021-web-auth-token-model.md) — bearer in
  WASM, no BFF, so this decision is **not** reopened. Establish service-worker cache-versioning
  discipline per release.

## Pros and cons of the options

### 1. Standalone WASM PWA
- 👍 First-class offline/PWA; mirrors MAUI Hybrid; clean API/client split.
- 👎 No SSR/SEO; first load heavier.

### 2. Blazor Web App + InteractiveWebAssembly + manual PWA
- 👍 Keeps a door open to server rendering later.
- 👎 PWA/offline setup has known friction in .NET 10; mixed render modes complicate offline reasoning.

### 3. Blazor Web App + Auto
- 👍 Best first-load performance.
- 👎 Server mode needs a live connection → **breaks offline-first**.

## Links

- arc42: [04 Solution strategy](../arc42/04-solution-strategy.md), [05 Building-block view](../arc42/05-building-block-view.md)
- Related: [ADR-0003](0003-modular-monolith-with-aspire.md), [ADR-0010](0010-api-style-and-versioning.md)
- Quality goal: Q1 local-first — [arc42 ch. 1](../arc42/01-introduction-and-goals.md)
