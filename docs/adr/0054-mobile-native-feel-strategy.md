---
status: Accepted
date: 2026-09-02
deciders: [Adrian]
tags: [ui, ux, mobile, maui, blazor, design]
---

# ADR-0054 — Mobile native-feel strategy (one Material design; feel earned by motion, ergonomics & OS integration — not platform chrome)

## Context and problem statement

[ADR-0040](0040-ui-component-library-mudblazor.md) settled the *component* question: one shared
MudBlazor (Material) UI, rendered by both WASM web hosts and the MAUI Blazor Hybrid heads, with a
neutral ("sobre") theme. In doing so it **already rejected** going fully native per platform (option 4)
and a platform-adaptive web kit that shows Material on Android and Cupertino on iOS (option 3), and it
named "a native-feeling mobile experience" as a driver — but only concretely as *bottom-tab
navigation, Material controls, and a light/dark switch*.

The MAUI head has since reached web parity ([UX-001](../roadmap/backlog.md)), installs on a physical
phone, and is heading for a backend-free first store release
([ADR-0053](0053-backend-free-first-mobile-release.md)). The maintainer's stated priority is now
explicit: **this is a mobile-first product, and the mobile version should get the most attention.**

An on-device/on-screen review raised the recurring question that ADR-0040 did not answer in full:

- The app is Material HTML in a WebView. On **Android** that reads fairly native; on **iOS** it will
  always look like "a Material app on iOS." Chasing true iOS-native chrome (Cupertino controls, grouped
  table views, action sheets, segmented controls, large titles, edge-swipe back) means either the
  platform-adaptive kit ADR-0040 rejected or a large, library-fighting custom-CSS effort.
- Meanwhile several genuinely native affordances that are *within* the Blazor-Hybrid frame are missing:
  haptics, pull-to-refresh, native page motion, action sheets, a FAB, local notifications, calendar and
  biometric integration, iOS App Links.

So, with mobile now the priority, a governing decision is needed so the backlog is built to a
consistent target rather than each item re-arguing "how native should this be?":

**How far should the mobile heads chase platform-native look-and-feel, and where should the "native
feel" effort go instead?**

## Decision drivers

- **Mobile-first priority** — the phone experience gets the most attention, so "feels native" must be
  defined well enough to build to.
- **Solo hobby project** — design-and-native-feel *per hour* matters more than per-platform fidelity
  ([ADR-0040](0040-ui-component-library-mudblazor.md) driver, unchanged).
- **One shared component set** renders on WASM + MAUI Hybrid, enforced by an architecture test
  ([ADR-0025](0025-shared-ui-building-blocks.md)); a decision that forks the UI per platform breaks it.
- **The biggest perceived-nativeness wins are cheap and cross-platform** — motion (haptics, transitions,
  pull-to-refresh), touch ergonomics, and OS integration (share, notifications, calendar, biometrics,
  deep links) — none of which require platform-divergent chrome.
- **Consistency of the neutral, restrained look** across web and both device heads is itself a product
  value (ADR-0040); a Cupertino/Material split works against it.
- Three languages (NFR-003) and accessibility (touch-target sizing) are ongoing constraints.

## Considered options

1. **Chase platform-native chrome** — introduce a platform-adaptive layer (Cupertino on iOS, Material on
   Android) via a web kit or heavy custom CSS, so each head matches its OS's controls.
2. **One neutral Material design on both heads; spend the native-feel budget on motion, touch ergonomics
   and OS integration** — keep the shared Material UI and make it feel native through behaviour, not
   platform-specific controls.
3. **Go fully native per platform** (MAUI XAML / Swift / Kotlin) — already considered and rejected by
   [ADR-0040](0040-ui-component-library-mudblazor.md); listed only to keep the axis complete.

## Decision outcome

**Chosen: option 2.** Keep **one neutral Material design across web and both device heads**, and treat
"native feel" as something earned through **motion, touch ergonomics, and OS integration** — explicitly
**not** through per-platform chrome. This extends ADR-0040 (it does not supersede it): 0040 chose the
component library and rejected the native/adaptive alternatives; this ADR sets the *target* the mobile
UX is built to.

Concretely, the decision commits the mobile work to these levers, in the priority the review proposed:

1. **Touch ergonomics** — every tappable element clears a ≥48 px target on mobile (continues the
   [R2-6 touch pass](../roadmap/backlog.md)); dense/small sizing is a desktop affordance, not a phone one.
2. **Navigation clean-up** — one menu entry point on narrow viewports (drop the redundant app-bar
   hamburger where the bottom bar's "More" already opens the drawer), and no degenerate one-item bottom
   bar in the signed-out state.
3. **Motion** — a **haptics** seam (a light buzz on draw, reveal, pin/delete) and **pull-to-refresh** on
   the list screens (triggering the existing `AutoSyncService`).
4. **Native interaction patterns within the Material vocabulary** — **bottom/action sheets** for
   destructive confirmations and overflow menus (replacing the inline `MudAlert` expanders), and a
   **floating action button** for the primary create/add actions.
5. **OS integration** — the already-selected distribution features (QR code, add-to-calendar, share-all;
   [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)), and the parked
   higher-design-cost ones (**local notifications**, **biometric app lock**) once each has its own
   design/ADR.
6. **iOS platform wiring** — iOS App Links (associated domains) and edge-swipe-back, to bring the iOS
   head to the Android head's deep-link parity when iOS becomes a target
   ([ADR-0031](0031-ios-build-host.md)).

The decision **explicitly declines**, and future work should not reopen without a new ADR:

- **iOS Cupertino chrome / a platform-adaptive UI kit** — the option ADR-0040 already rejected; the cost
  (a non-.NET toolchain or library-fighting CSS, and a forked look) is not justified for a solo project,
  and it works against the deliberate cross-platform consistency.
- **Material You / dynamic colour** — rejected in the [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)
  as conflicting with the neutral chrome ([ADR-0040](0040-ui-component-library-mudblazor.md)); unchanged here.
- **Platform-divergent layouts** — the shared shell stays one layout responsive to width, not a per-OS fork.

The near-term, implementable subset (levers 1–4) is specified as
[FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md); levers 5–6 remain backlog/brainstorm items,
each taken through the process if pursued.

## Consequences

- **Positive:** a single, coherent target for all mobile UX work; the highest perceived-nativeness wins
  (motion, ergonomics, OS integration) are unblocked and cheap; the shared-UI architecture and the neutral
  look are preserved; effort is not spent fighting the library for iOS chrome that would still read as a
  web app.
- **Negative / accepted:** on **iOS** the app looks like polished Material, not a Cupertino-native app —
  a deliberate trade. Users who expect iOS-native controls will notice; the bet is that motion +
  ergonomics + integration matter more to a family gift-exchange app than control styling.
- **Negative / accepted:** some native behaviours (haptics, pull-to-refresh, action sheets) still need
  per-platform seams or JS interop even though the *look* is shared — modest, isolated platform code
  behind ports, in the [ADR-0028](0028-browser-platform-adapters-block.md) style already used for
  `IShareService`.
- **Follow-ups:**
  1. Build [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (levers 1–4).
  2. Write the design/ADR for **local notifications** (scheduling + permission model) and **biometric app
     lock** (opt-in; NFR-004 privacy review) before building them.
  3. Wire **iOS App Links + edge-swipe-back** when the iOS head becomes a target
     ([ADR-0031](0031-ios-build-host.md)).
- **New risks:** low. The main risk is scope creep back toward per-platform chrome; this ADR exists to
  make that a conscious re-decision rather than a drift.

## Pros and cons of the options

### 1. Chase platform-native chrome
- 👍 Best per-platform fidelity; an iOS user gets Cupertino controls.
- 👎 Reopens the option ADR-0040 rejected; non-.NET toolchain or heavy library-fighting CSS; forks the
  look and undercuts cross-platform consistency; disproportionate for a solo project.

### 2. One Material design; feel via motion, ergonomics & integration (chosen)
- 👍 Keeps the shared UI and neutral look; unblocks the cheap, high-impact wins; consistent target for the
  whole backlog; native OS integration is orthogonal to chrome.
- 👎 iOS never looks Cupertino-native; a few behaviours still need small per-platform seams.

### 3. Fully native per platform
- 👍 Best native fidelity and gestures out of the box.
- 👎 Abandons the shared-UI architecture; ~2–3× ongoing UI work — already rejected by ADR-0040.

## Links

- Extends: [ADR-0040](0040-ui-component-library-mudblazor.md) (MudBlazor / Material; rejected native &
  adaptive alternatives)
- Related ADRs: [ADR-0025](0025-shared-ui-building-blocks.md) (one shared UI set),
  [ADR-0028](0028-browser-platform-adapters-block.md) (platform-seam pattern the native behaviours reuse),
  [ADR-0031](0031-ios-build-host.md) (iOS build host — gates the iOS wiring),
  [ADR-0053](0053-backend-free-first-mobile-release.md) (the v1 release this polishes)
- Feature: [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md)
- Brainstorm: [Secret-Santa mobile features (2026-09-01)](../brainstorming/2026-09-01-secret-santa-mobile.md)
- arc42: [04 Solution strategy](../arc42/04-solution-strategy.md)
- Requirements: [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
