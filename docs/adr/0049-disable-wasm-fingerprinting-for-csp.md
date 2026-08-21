---
status: Accepted
date: 2026-08-21
deciders: [Adrian]
tags: [wasm, csp, security, deployment, blazor, incident]
---

# ADR-0049 — Disable WASM asset fingerprinting for strict-CSP compatibility

## Context and problem statement

The first CI/CD deploy after [ADR-0047](0047-versioning-and-release-strategy.md)/[ADR-0048](0048-web-app-update-delivery.md)
took the **Secret-Santa** web app down: every fresh browser session hung on the loading splash and the
WASM runtime never started. The Main app was unaffected. The browser console showed the cause:

```
Executing inline script violates the following Content Security Policy directive
'script-src 'self' 'wasm-unsafe-eval''. … a hash (…) or a nonce (…) is required …
→ Failed to start platform. Reason: Failed to fetch dynamically imported module: /_framework/dotnet.js
```

.NET's static-asset **fingerprinting** gives runtime files content-hashed names (`dotnet.<hash>.js`)
and emits an **inline `<script type="importmap">`** to map the bare `dotnet.js` / `blazor.webassembly.js`
specifiers onto them. The suite's **strict CSP** ([ADR-0021](0021-web-auth-token-model.md), finalized in
the Caddyfile) sets `script-src 'self' 'wasm-unsafe-eval'` with **no `'unsafe-inline'`, nonce or hash**,
so the browser **blocks the import map**; the loader's `import('./dotnet.js')` then 404s (the bare name
doesn't exist, only the hashed one) and boot dies.

The real trigger was **configuration drift between the two WASM heads**: Main had `<OverrideHtmlAssetPlaceholders>false</OverrideHtmlAssetPlaceholders>`
(fingerprinting off, no import map) with a csproj comment explaining exactly this, while Secret-Santa
had it `true`. Returning users didn't notice because the offline service worker served the cached older
build; the break only surfaced on a fresh session (a private window). So: **how do the standalone WASM
heads boot under a strict, inline-free CSP, and how do we stop the two heads from drifting again?**

## Decision drivers

- **Keep the strict, inline-free CSP** — no `'unsafe-inline'` in `script-src` ([ADR-0021](0021-web-auth-token-model.md)); it is the XSS mitigation.
- **Static hosting** — the apps are static WASM served by Caddy ([ADR-0036](0036-cloud-provider-and-hosting.md)); there is no per-request server render to inject a nonce or a per-build hash.
- **Deterministic, low-maintenance** — no per-build value that a static config must chase.
- **Both heads identical** — the drift that caused the outage must not be able to recur silently.

## Considered options

1. **Disable framework-asset fingerprinting** (`OverrideHtmlAssetPlaceholders=false`) on both heads → no
   import map, stable asset names, boots under the strict CSP as-is.
2. **Keep fingerprinting, allow the import map with its SHA-256 hash** in `script-src`.
3. **Keep fingerprinting, allow the import map with a nonce**.
4. **Add `'unsafe-inline'` to `script-src`**.

## Decision outcome

**Chosen: option 1 — disable fingerprinting on both WASM heads.** `OverrideHtmlAssetPlaceholders=false`
(the setting Main already used) turns off the import map and the hashed names; `index.html` then
references the plain `blazor.webassembly.js` and carries **no** import map element. Secret-Santa is
brought in line with Main, and the divergence is the root cause we remove.

- Option 2 (hash) is defeated by static hosting: the import map embeds fingerprints, so its hash
  **changes every build**, and a static CSP in the Caddyfile cannot track it without build-time
  injection of a per-app hash — fragile, and exactly the kind of moving part that broke us.
- Option 3 (nonce) needs a **per-request server render** to emit a fresh nonce into both the header and
  the tag; our heads are static files, so there is nowhere to mint one.
- Option 4 (`'unsafe-inline'`) throws away the CSP's XSS protection ([ADR-0021](0021-web-auth-token-model.md)) — rejected.

**Guardrails against re-drift:** both heads carry the same explaining comment in their `.csproj`, and
the incompatibility is recorded here and in [ADR-0048](0048-web-app-update-delivery.md). Verification of
this class of defect must be done against a **published** build served under the production CSP — a
`dotnet run` uses a no-op dev service worker and no CSP, so it cannot reproduce it (see the
Consequences of [ADR-0048](0048-web-app-update-delivery.md)).

## Consequences

- **Positive:** both heads boot under the strict CSP with a clean console; the CSP stays inline-free; no
  per-build value for a static config to chase.
- **Negative / accepted:** **no content-hashed asset URLs**, so the long-lived `immutable` HTTP cache is
  not available — assets are served `no-cache` (ETag revalidation). Cheap, and largely moot because the
  **service worker** serves repeat visits from its own content-hashed cache offline. This **corrects**
  [ADR-0048](0048-web-app-update-delivery.md), whose original `immutable` header on `/_framework/*` would
  have served stale files once fingerprinting was off.
- **Negative / accepted:** we forgo a framework default (fingerprinting); if a future .NET makes the
  import map avoidable under CSP another way, revisit.
- **Follow-ups:** if fingerprinting is ever wanted back (e.g. for immutable caching), it requires a
  per-request nonce/hash mechanism, which means a non-static host for the HTML — a larger change.
- **New risks:** the two heads must stay in sync; the shared csproj comment + these ADRs are the
  mitigation. A CI check that fails on an inline import map in a published `index.html` would be a
  stronger guard (possible follow-up).

## Pros and cons of the options

### 1. Disable fingerprinting (chosen)
- 👍 One static setting; boots under the strict CSP; deterministic; matches Main.
- 👎 Loses content-hashed URLs and the `immutable` HTTP cache (mitigated by the service worker).

### 2. Import-map SHA-256 hash in CSP
- 👍 Keeps fingerprinting and immutable caching.
- 👎 Hash changes every build; a static Caddy CSP can't track it without fragile build-time injection (two heads = two hashes).

### 3. Import-map nonce
- 👍 Standard CSP mechanism; keeps fingerprinting.
- 👎 Needs a per-request server render to mint the nonce; our heads are static files.

### 4. `'unsafe-inline'`
- 👎 Guts the CSP's XSS protection ([ADR-0021](0021-web-auth-token-model.md)).

## Links

- Related ADRs: [ADR-0021](0021-web-auth-token-model.md) (strict CSP), [ADR-0048](0048-web-app-update-delivery.md)
  (offline PWA + caching — corrected here), [ADR-0005](0005-web-hosting-render-model.md) (standalone WASM PWA),
  [ADR-0036](0036-cloud-provider-and-hosting.md) (Caddy edge), [ADR-0047](0047-versioning-and-release-strategy.md)
  (the deploy that surfaced this)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md)
