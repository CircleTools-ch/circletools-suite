---
status: Accepted
date: 2026-08-21
deciders: [Adrian]
tags: [pwa, service-worker, offline, deployment, ux, caching, web]
---

# ADR-0048 — Web app update delivery (offline PWA + reload prompt)

## Context and problem statement

Both web heads are **standalone Blazor WASM PWAs** ([ADR-0005](0005-web-hosting-render-model.md)) with the
stock Blazor **offline service worker**. After the first deploy under CI/CD
([ADR-0047](0047-versioning-and-release-strategy.md)), a returning user kept seeing the **old** version —
only a fresh private window showed the new one. That is not a caching accident; it is exactly how the
stock service worker behaves:

- Its `onFetch` is **cache-first for everything**, including `index.html` and the whole `_framework`, so
  even a normal refresh is served from the cache.
- A new deploy *is* detected (`updateViaCache: 'none'` re-fetches the worker and its imported
  `service-worker-assets.js`, whose version changes each build), but the template **never calls
  `skipWaiting()`/`clients.claim()`**, so the new worker installs and then **parks in the "waiting"
  state until every tab of the site is closed** — and the user is **never told** an update is waiting.

So the question: **how should a newly deployed version reach an already-open web app**, without
sacrificing the offline capability that is core to the product? Two constraints frame it:

- **Local-first is a product promise.** The apps work offline with no account
  ([ADR-0015](0015-mvp-client-offline-storage.md)); the offline app shell must keep loading with no
  network. Any option that removes offline support fails the product, not just the feature.
- **The web and the store apps differ fundamentally.** The MAUI heads bake their assets into the
  APK/AAB and update through the store ([ADR-0046](0046-suite-packaging-and-app-composition.md),
  [ADR-0047](0047-versioning-and-release-strategy.md)); there is no browser cache to bust. **This is a
  web-PWA problem only** — the store handles the native heads.

## Decision drivers

- **Preserve offline / local-first** — the app shell must still load with no network.
- **Tell the user** a new version exists, and let them **switch deliberately** — never lose in-progress
  work (a draw, a half-filled form) to a surprise reload.
- **Shared across both web heads**, and a no-op on the MAUI head (no service worker there).
- **Prompt within the app's own visual language** (MudBlazor, [ADR-0040](0040-ui-component-library-mudblazor.md)),
  localized DE/EN/FR ([ADR-0011](0011-localization-format-and-culture.md)).
- **Correct HTTP caching** at the edge as defense-in-depth ([ADR-0036](0036-cloud-provider-and-hosting.md)).

## Considered options

1. **Keep the offline PWA + an "Update available" reload prompt.** Add a `SKIP_WAITING` message handler
   to the worker; detect the waiting worker in JS and surface a MudBlazor snackbar with a **Reload**
   action that activates it and reloads.
2. **Silent auto-update.** Worker calls `skipWaiting`/`clients.claim`; the page auto-reloads on
   `controllerchange`.
3. **Drop the service worker.** Serve as a plain SPA with correct cache headers; updates arrive on the
   next refresh.
4. **App-level version poll.** The running app polls a `/version` endpoint and prompts on mismatch.

## Decision outcome

**Chosen: option 1 — keep the offline PWA and add a reload prompt.**

The offline shell stays (rejecting option 3, which trades away a core local-first promise to fix an
update-UX bug). The user is **notified and chooses when to switch**, so a background deploy never
interrupts an in-progress draw or form (rejecting option 2's surprise reload). Option 4 reinvents,
without the service worker, an update signal the worker lifecycle already provides.

**Mechanism** (shared; a no-op on MAUI):

- **Service worker** (`service-worker.published.js`, both heads): add a `message` handler that calls
  `self.skipWaiting()` on `{ type: 'SKIP_WAITING' }`.
- **Registration bridge** (`js/app.js`, both heads): `window.circletoolsUpdate` attaches to the
  existing registration via `navigator.serviceWorker.ready` (it does **not** register twice — offline
  registration stays at startup), notifies .NET when a worker reaches `installed` **while a controller
  exists** (an update, not the first install), calls `registration.update()` on `visibilitychange` so
  long-lived tabs discover deploys, and reloads once on `controllerchange`.
- **Prompt** (`CircleTools.Ui.Shared/UpdatePrompt.razor`, mounted in both shells): on first render it
  wires the JS→.NET callback; on notification it shows a **RequireInteraction** MudBlazor snackbar —
  *"A new version is available."* with a **Reload** action (localized DE/EN/FR). Reload posts
  `SKIP_WAITING`; the resulting `controllerchange` reloads into the new assets. The close icon is the
  implicit "Later". On the MAUI head the global is absent, so the interop call is guarded and does
  nothing.

**Edge caching** (`Caddyfile`, [ADR-0036](0036-cloud-provider-and-hosting.md)): two non-overlapping
matchers make precedence unambiguous — `/_framework/*` (the fully **fingerprinted** WASM payload: the
loader, runtime and every assembly carry a content hash in the file name under .NET 10) is
`public, max-age=31536000, immutable`; **everything else** — `index.html`, the worker,
`service-worker-assets.js`, the manifest, and SPA-fallback navigations — is `no-cache` (revalidate), so
a deploy is seen promptly. Defense-in-depth; the worker's `updateViaCache: 'none'` already bypasses the
HTTP cache for its own update check.

**Android / iOS:** unchanged. Store updates deliver new versions; an in-app store-update nudge is a
possible later refinement, tracked separately, not part of this ADR.

## Consequences

- **Positive:** offline/local-first intact; users learn about a new version and switch with one tap,
  with no interruption to in-progress work. One shared component covers both web heads and is inert on
  MAUI. The version stamp ([ADR-0047](0047-versioning-and-release-strategy.md)) already tells a user
  which build they are on; the prompt tells them a newer one is ready.
- **Negative / accepted:** the update path only runs in a **published** build — in Development the
  service worker is the no-op dev stub, so this must be verified against a published/deployed build, not
  `dotnet run`. Noted for future UI verification of this area.
- **Negative / accepted:** the worker and the `circletoolsUpdate` glue are **duplicated per web head**
  (each PWA owns its `wwwroot`), like the existing service worker and `app.js`. Kept byte-identical; the
  shared C# lives in `Ui.Shared`.
- **Follow-ups:** consider showing the **incoming version number** in the prompt (needs the new
  worker's manifest version surfaced to the page); consider an **in-app store-update** nudge for the
  native heads ([ADR-0046](0046-suite-packaging-and-app-composition.md)).
- **New risks:** none material. A user who ignores the prompt keeps running the previous version safely
  (offline-first), exactly as before — the change only *adds* a way forward.

## Pros and cons of the options

### 1. Offline PWA + reload prompt (chosen)
- 👍 Keeps offline; user-controlled switch; no lost work; standard PWA pattern; app-native prompt.
- 👎 Slightly more moving parts (worker message + JS bridge + component) than doing nothing.

### 2. Silent auto-update
- 👍 Always current with no UI.
- 👎 A surprise reload can interrupt a draw / half-filled form — wrong for this app's flows.

### 3. Drop the service worker
- 👍 Simplest; updates on next refresh; correct with cache headers.
- 👎 **Removes offline load of the app shell** — rejects the local-first promise
  ([ADR-0015](0015-mvp-client-offline-storage.md)).

### 4. App-level version poll
- 👍 Works without a service worker.
- 👎 Reinvents the worker's own update signal; extra endpoint and polling for no gain here.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md) (standalone WASM PWA),
  [ADR-0015](0015-mvp-client-offline-storage.md) (offline storage / local-first),
  [ADR-0047](0047-versioning-and-release-strategy.md) (versioning — the build the prompt switches from),
  [ADR-0040](0040-ui-component-library-mudblazor.md) (MudBlazor snackbar),
  [ADR-0028](0028-browser-platform-adapters-block.md) (browser glue lives with the web heads),
  [ADR-0036](0036-cloud-provider-and-hosting.md) (Caddy edge / cache headers),
  [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md) (the connectivity bridge this
  sits beside)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md)
