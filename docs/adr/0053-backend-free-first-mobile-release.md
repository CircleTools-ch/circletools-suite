---
status: Accepted
date: 2026-09-01
deciders: [Adrian]
tags: [deployment, mobile, android, hosting, deep-links, offline, release]
---

# ADR-0053 — Backend-free first mobile release (reveal hosting, origin config & App Links)

## Context and problem statement

The first Secret-Santa release to Google Play should work **without the API backend**: the whole
organizer journey — create → members → exclusions → draw → **share each person a reveal link** — and
**opening a shared link** must work with no account and no server. The account API at
`api.circletools.ch` should become necessary **only when someone logs in to sync** (a v2 concern).

Most of the pieces already exist:

- The offline product needs no server ([ADR-0015](0015-mvp-client-offline-storage.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)); reveal links are decoded
  **entirely client-side** from the URL fragment ([ADR-0014](0014-reveal-link-encoding-and-privacy.md)),
  never sent to a server.
- The web client is a **standalone Blazor WASM PWA** — static files ([ADR-0005](0005-web-hosting-render-model.md)).
- The MAUI head is **self-contained**: its Razor UI is compiled into the APK/AAB and served by a local
  WebView, updated **only through the store** ([ADR-0046](0046-suite-packaging-and-app-composition.md),
  [ADR-0047](0047-versioning-and-release-strategy.md)). Publishing a new Web UI therefore never changes
  an installed app — it keeps the version it was built with. That is the intended behaviour.
- Hosting is decided and **live**: an Infomaniak VPS with Caddy ([ADR-0036](0036-cloud-provider-and-hosting.md)).
  `deploy/Caddyfile` already serves `secretsanta.circletools.ch` as a static WASM PWA (SPA fallback,
  auto-HTTPS), and `appsettings.Production.json` already sets that origin (with `RevealBaseUrl:""` →
  the app's own origin).

Two gaps remain between this and a shippable, *useful* backend-free v1:

1. The **MAUI head hardcodes `RevealBaseUrl`** to a dev/LAN value (`MauiProgram.cs`), and the store
   workflow (`release-mobile.yml`) overrides only the version numbers. A store build today would emit
   reveal links pointing at a developer's LAN — useless to a recipient.
2. The maintainer wants an opened link, **on a device that has the app**, to **open the app and render
   the reveal offline** — not force a browser round-trip to a server.

**What must ship for a backend-free v1, and how are reveal links delivered without the API?**

## Decision drivers

- **Backend-free.** Everything but login/sync works with no API (NFR-001); `api.circletools.ch` is
  touched only on an explicit login.
- **Self-contained app.** The installed app renders from its **baked-in** UI; a later Web UI deploy must
  not alter it. Store-only updates (ADR-0047).
- **Links reach every recipient** — whether or not they have the app installed.
- **Permanent origin.** The reveal origin is fixed to `secretsanta.circletools.ch`
  ([naming.md](../process/naming.md), ADR-0036) and baked into store builds.
- **Store-distributable** ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
  [ADR-0030](0030-mobile-store-publisher-identity.md)).

## Considered options

For **how a shared reveal link is delivered**:

1. **Web-only** — links always open the static WASM page at `secretsanta.circletools.ch`.
2. **Static web page + Android App Links** — an installed app opens the link and renders it offline from
   its baked UI; a recipient without the app falls back to the same static page.
3. **Custom URI scheme** (`secretsanta://…`) to open the app.

## Decision outcome

**Chosen: option 2 — the live static page as the universal fallback, plus Android App Links so the
installed app handles reveal links offline.** Both paths are backend-free; the API is never involved in
a reveal. Concretely:

**a. `RevealBaseUrl` becomes a build-time setting on the MAUI head.** Replace the hardcoded const with a
value injected at build via an MSBuild property surfaced through an `AssemblyMetadata` attribute and read
at startup. Defaults: **Debug** = the dev LAN address (local device testing); **Release** =
`https://secretsanta.circletools.ch`. `release-mobile.yml` passes `-p:RevealBaseUrl=…` alongside the
version props it already sets, so a store build always bakes the production origin. (The web head is
already correct via `appsettings.Production.json`.)

**b. Android App Links for `secretsanta.circletools.ch/r`.** Add an `autoVerify` intent filter
(`AndroidManifest.xml`) for `https` host `secretsanta.circletools.ch`, path prefix `/r`; have
`MainActivity` capture the incoming URL and navigate the Blazor WebView to the app's own `/r#…` route so
`Reveal.razor` renders it **from the baked UI, offline**. Serve
`https://secretsanta.circletools.ch/.well-known/assetlinks.json` (a static file on the existing host —
no backend) listing package `ch.circletools.secretsanta` and the app's signing SHA-256.

**c. Non-installers** open the same URL in a browser → the static WASM reveal page renders it
client-side. Also offline/backend-free.

**d. Accounts unchanged.** The sign-in/sync UI stays optional and inert until used; it reaches
`api.circletools.ch` only on an explicit login, which is a v2 feature. `api.circletools.ch` need not be
deployed for v1.

**Why version drift between the baked app and the live web page is safe:** the reveal payload is
**versioned** (`v=` in the fragment, `RevealPayload.CurrentVersion`, ADR-0014). An older installed app
decodes any payload of a version it understands and renders it with its own (older) UI; only cosmetics
differ, never correctness. So the app keeping its build-time version — exactly as intended — cannot
"break" against newer links.

**Scope.** *In v1:* offline product, native share sheet ([ADR-0028](0028-browser-platform-adapters-block.md)
amendment), reveal via App Links + web fallback, the `RevealBaseUrl` build config. *Deferred to v2+:*
the API / accounts / sync deployment, iOS ([ADR-0031](0031-ios-build-host.md)), and the brainstorm
follow-ups (QR code, add-to-calendar, share-all — see
[mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)).

## Consequences

- **Positive:** v1 ships with **zero server dependency**; installed users get the app opening links
  offline, non-installers get the offline web page, and the app is immutable per build (the maintainer's
  model). The reveal origin is baked correctly into store builds instead of a dev URL.
- **Negative / accepted:** App Link **auto-verification depends on the signing certificate** — the
  `assetlinks.json` SHA-256 comes from **Play App Signing**, known only after the first upload
  ([ADR-0030](0030-mobile-store-publisher-identity.md) store-account follow-up). Until signing exists,
  links open via the system chooser rather than silently in the app — functional, not yet seamless.
- **Negative / accepted:** reveal links are long (~200-char blob in the fragment, ADR-0014). Static
  hosting carries this fine; noted as an existing constraint, not introduced here.
- **Follow-ups:**
  1. Implement (a)–(c) (separate build step): the `RevealBaseUrl` seam + `release-mobile.yml` flag; the
     intent filter + `MainActivity` link handling; `assetlinks.json` under `deploy/` served by Caddy
     (confirm `file_server` serves `/.well-known/…` before the SPA `try_files` fallback).
  2. Complete Play signing/enrolment (ADR-0030) → fill the real SHA-256 → App Link auto-verify passes.
  3. Optional UX nicety: in a store build with no reachable API, present the account entry as “sync
     coming soon” rather than a login that errors (keeps the “no dead ends” posture of UX-002/UX-005).
- **New risks:** a wrong fingerprint in `assetlinks.json` silently falls back to the browser/chooser
  (annoyance, not data risk). Mitigate by verifying with Google's Statement List tester after signing.

## Pros and cons of the options

### 1. Web-only
- 👍 Simplest; nothing new on the app; already works via the live static host.
- 👎 An installed user gets a browser tab, not the app; misses the "open in the app, offline" experience
  the maintainer wants.

### 2. Static page + App Links (chosen)
- 👍 Installed app opens links offline from its baked UI; browser fallback covers everyone else; both
  backend-free; standard, verifiable mechanism.
- 👎 Needs an intent filter, in-app link handling, and an `assetlinks.json` whose verification waits on
  the signing cert.

### 3. Custom URI scheme
- 👍 Opens the app with no domain verification.
- 👎 `secretsanta://` isn't clickable/reliable in WhatsApp/SMS/mail, and has **no browser fallback** for
  recipients without the app — the majority. Rejected.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md), [ADR-0014](0014-reveal-link-encoding-and-privacy.md),
  [ADR-0015](0015-mvp-client-offline-storage.md), [ADR-0028](0028-browser-platform-adapters-block.md),
  [ADR-0030](0030-mobile-store-publisher-identity.md), [ADR-0036](0036-cloud-provider-and-hosting.md),
  [ADR-0046](0046-suite-packaging-and-app-composition.md), [ADR-0047](0047-versioning-and-release-strategy.md),
  [ADR-0048](0048-web-app-update-delivery.md)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md)
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
- Process / deployment: [naming.md](../process/naming.md), [mobile apps runbook](../deployment/mobile-apps.md),
  `deploy/Caddyfile`
