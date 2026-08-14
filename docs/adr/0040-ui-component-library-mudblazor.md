---
status: Accepted
date: 2026-08-13
deciders: [Adrian]
tags: [architecture, ui, frontend, design, maui, blazor]
supersedes: [0024]
---

# ADR-0040 — UI component library: MudBlazor (Material), supersedes ADR-0024

## Context and problem statement

[ADR-0024](0024-ui-styling-and-theming.md) chose **Bootstrap 5 + a thin suite theme** while the brand
was deliberately undecided, and explicitly **deferred component libraries** until the brand landed —
its own *Follow-ups* said to revisit at that point. The brand has since landed (the
[naming](../process/naming.md) visual identity: a ring-of-five mark, per-app accents), and building
the first themed pass surfaced what the Bootstrap approach does **not** give cheaply:

- A **coherent, modern look with little design effort.** Bootstrap screens read as Bootstrap; making
  them feel like a designed product is per-screen CSS work.
- **A native-feeling mobile experience.** The MAUI heads are Blazor Hybrid — the UI is HTML in a
  WebView — and the app must feel like an Android/iOS app (bottom-tab navigation, Material controls,
  a real light/dark switch), with **push notifications** coming later on the native shell.
- **First-class theming.** A single seed accent, light/dark palettes, and a runtime theme switch,
  without hand-rolling a design system.

The product is **one shared Razor UI** (`Ui.Shared`) rendered by both WASM web hosts and the MAUI
Blazor Hybrid heads — enforced by an architecture test ([arc42 §5](../arc42/05-building-block-view.md),
[ADR-0025](0025-shared-ui-building-blocks.md)). So the question is a suite-wide one:

**What styling/component approach gives a modern, native-feeling, themeable UI across web and the
device heads, from one shared component set — and is it worth reversing ADR-0024 to get it?**

A prior question — *should the device heads go fully native (per-platform MAUI XAML / Swift / Kotlin)
instead of Blazor Hybrid?* — was considered and **rejected**: it would abandon the shared-UI
architecture and roughly 2–3× the ongoing UI work for a solo hobby project. Notifications do **not**
require it — the native MAUI shell owns them regardless of how the content is rendered. This ADR
therefore keeps Blazor Hybrid and changes only the component/styling layer.

## Decision drivers

- Solo hobby project: **design polish per hour** matters more than avoiding a dependency.
- One shared component set must render on **WASM and MAUI Blazor Hybrid**.
- **Native-feeling navigation** (bottom tabs on mobile, drawer/rail on wide) and Material controls.
- **Built-in light/dark + a runtime theme switch**, persisted per device.
- Three languages (NFR-003): components must tolerate longer DE/FR text.
- Accessibility remains a stated criterion (TP-002).
- The brand is now decided; the *in-app* look should be **restrained ("sobre") and neutral** with one
  light accent, keeping the festive per-app brand (burgundy/gold for Secret-Santa) on **icon, splash
  and store/marketing assets** — not on every surface.

## Considered options

1. **Keep Bootstrap 5 + thin theme (ADR-0024), restyle by hand** to a sober neutral look.
2. **MudBlazor** — a Material Design component library for Blazor.
3. **A platform-adaptive web UI kit** (Material on Android, Cupertino on iOS; e.g. Konsta/Framework7).
4. **Go fully native per platform** (MAUI XAML or native) — out of scope per the context above.

## Decision outcome

**Chosen: option 2 — MudBlazor**, adopted suite-wide from `Ui.Shared`, with:

- **A neutral, "sobre" theme** with a single calm accent (a desaturated indigo/blue in the suite
  family), defined once as a `MudTheme` with light and dark palettes. The per-app festive brand stays
  on the app icon, splash and store/marketing assets only; `naming.md` is unchanged.
- **An in-app System/Light/Dark switch**, persisted through the existing `IKeyValueStore` seam
  ([ADR-0028](0028-browser-platform-adapters-block.md)) so it works on web (localStorage) and device
  (file), and mirrored to `Application.UserAppTheme` on MAUI so the native chrome follows.
- **A native-feeling shared shell**: `MudLayout` + a bottom navigation bar on narrow viewports and a
  drawer/rail on wide, with safe-area insets and hardware-back wired to Blazor navigation.

MudBlazor over the alternatives: it is **.NET-native** (Razor components, no JS build step), works
unchanged in WASM and MAUI Hybrid, and ships the theming, light/dark and Material controls the drivers
ask for. Option 3 gives the best *per-platform* fidelity but adds a non-.NET toolchain and more custom
wiring for a solo project; option 1 keeps the dependency count low but leaves exactly the design and
native-feel work that motivated this decision.

This **supersedes ADR-0024.** Its layering idea survives in spirit (one shared theme; app-specific and
component-local styling stay separated), but Bootstrap and the `circletools.css` Bootstrap-skin layer
are retired as screens migrate.

### Rollout

Carried out in two phases within the same change:

- **Phase 1 (done):** foundation (package, services, theme, shell) + migrated **Secret-Santa** and the
  shared **account** screens, so the flagship never shipped half-styled.
- **Phase 2 (done):** migrated the **Main** app onto the shared MudBlazor shell, relaxed its CSP
  (below), and removed Bootstrap suite-wide (the `lib/bootstrap` assets, `circletools.css`
  Bootstrap-skin layer — slimmed to a screen-reader-only helper — and the `theme.js`/`data-bs-theme`
  bridges). The one shared component set (`Ui.Shared`/`Ui.Account`) is hosted by both apps, so once
  those components became MudBlazor the Main host had to adopt the Mud runtime too, which is why both
  phases landed together rather than Phase 2 being deferred.

## Consequences

- **Positive:** a modern, consistent, accessible UI for little bespoke CSS; native-feeling navigation
  and a real theme switch from one shared component set; light/dark for free.
- **Negative / accepted — dependency & lock-in.** ADR-0024 valued Bootstrap's cheap exit (markup is
  plain HTML). MudBlazor's markup *is* the library, so leaving it later means rewriting screens. This
  is the deliberate trade ADR-0024 said to reconsider once the brand justified the investment.
- **Negative / accepted — CSP relaxation on the Main web host.** `MudThemeProvider` emits a **dynamic
  inline `<style>`** for the theme. The standalone WASM host cannot set a per-request nonce, so the
  Main app's `style-src` is now `'self' 'unsafe-inline'`
  ([ADR-0021](0021-web-auth-token-model.md)'s CSP, in `Main.WebApp/wwwroot/index.html`). Inline
  **script** stays forbidden (`script-src` keeps only `'self' 'wasm-unsafe-eval'`) — the primary XSS
  mitigation is unchanged; only inline *style* is permitted. The Secret-Santa hosts serve no CSP meta,
  so they were unaffected.
- **Negative / accepted — payload.** MudBlazor's CSS/JS add to the WASM download. Acceptable for the
  value; fonts are a **system-ui stack** (no Google Fonts) to respect `font-src 'self'` and keep it
  light.
- **Follow-ups:** wire push notifications on the native shell when that work starts; tune the accent
  and Android/iOS status-bar treatment on real devices (the neutral accent `#5b7cc4` is the agreed
  starting point). *(Screen migration, Bootstrap removal, and the bUnit assertion updates are done.)*
- **Retired risk:** a half-migrated app mixing Mud + Bootstrap — avoided by migrating every screen in
  both apps together and removing Bootstrap in the same change; the full test suite (incl. the
  ArchUnit layering tests) is green.

## Amendment (2026-08-14) — per-app app-bar accent, brand mark & a cross-app switcher

The first hands-on pass with the shared shell showed that a fully neutral chrome makes the suite's apps
hard to tell apart, and that there was no in-product way to move between them on the web. Two narrow
additions, both staying inside this ADR's layering:

- **The app bar — and only the app bar — carries the per-app festive accent**, plus the app's brand
  mark to its left. This narrows, but does not overturn, the "in-app surfaces stay neutral" rule: every
  other surface (drawer, cards, content) is still the shared neutral palette with the single calm
  accent. Only the top bar takes the app colour (Secret-Santa burgundy `#7a1f2b` on cream `#f7ede1`;
  Main brand navy `#03173d`), as an identity cue rather than a themed surface — so it is the **same in
  light and dark**. Implemented as `CircleToolsTheme.ForApp(AppBranding)`, which clones the neutral
  light/dark palettes and overrides only `AppbarBackground`/`AppbarText`; `AppBranding` is registered
  once per app in its UI composition root and injected by the shared shell (web + MAUI). The brand marks
  are served **same-origin** from the shared building block (`_content/CircleTools.Ui.Shared/img/`), so
  they resolve under the existing `img-src 'self'` CSP on every head.
- **A cross-app switcher in the app bar (web only)** and matching links on the Main dashboard, so a
  visitor can move between the suite's apps. Each app is a separate WASM origin (ADR-0013), so a switch
  is a full navigation, not routing. The catalogue is host configuration (`SuiteApps` in
  `wwwroot/appsettings.json`), registered as `SuiteAppLink`s; MAUI heads register none, so the switcher
  self-hides there. No new cross-origin CSP allowance is needed — logos are the same same-origin shared
  assets, and the links are ordinary top-level navigations.

Neither touches `script-src`; the CSP script posture from the main decision is unchanged.

## Pros and cons of the options

### 1. Keep Bootstrap + hand restyle
- 👍 No new dependency; cheap exit; already in the repo.
- 👎 Leaves the design and native-feel work undone; every screen is bespoke CSS.

### 2. MudBlazor (chosen)
- 👍 .NET-native; one component set for WASM + MAUI; theming, light/dark, Material controls built in.
- 👎 Library lock-in (markup is the library); forces the Main CSP relaxation; larger payload.

### 3. Platform-adaptive web kit (Konsta/Framework7)
- 👍 Best *per-platform* look (Material on Android, Cupertino on iOS) while staying hybrid.
- 👎 Non-.NET toolchain; more custom wiring; less integrated with Blazor than MudBlazor.

### 4. Fully native per platform
- 👍 Best native fidelity and gestures out of the box.
- 👎 Abandons the shared-UI architecture; ~2–3× ongoing UI work; unjustified for a solo project.
  Notifications do not require it.

## Links

- Supersedes: [ADR-0024](0024-ui-styling-and-theming.md)
- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md) (standalone WASM),
  [ADR-0021](0021-web-auth-token-model.md) (the CSP this relaxes),
  [ADR-0025](0025-shared-ui-building-blocks.md) (where the shared UI lives),
  [ADR-0028](0028-browser-platform-adapters-block.md) (the `IKeyValueStore` seam theme persistence
  reuses)
- arc42: [04 Solution strategy](../arc42/04-solution-strategy.md),
  [05 Building-block view](../arc42/05-building-block-view.md)
- Requirements: [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Process: [naming § Visual identity](../process/naming.md)
