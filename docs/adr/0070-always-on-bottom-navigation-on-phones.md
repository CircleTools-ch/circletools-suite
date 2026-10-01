---
status: Accepted
date: 2026-10-01
deciders: [Adrian]
tags: [ui, mobile, navigation, ux, secret-santa]
---

# ADR-0070 — Bottom navigation in the native app (phone + iPad portrait)

## Context and problem statement

[ADR-0054](0054-mobile-native-feel-strategy.md) set the mobile navigation to a bottom tab bar on narrow
viewports, with a navigation-cleanup lever: **"no degenerate one-item bottom bar in the signed-out
state."** The shell implemented that by showing the bar only when it carried more than the single
always-present "Groups" tab (`_showBottomBar => _bottomNav.Count > 1`), i.e. **only for a signed-in linked
member**. On-device testing showed the cost: the primary navigation **changed shape on sign-in**, and the
signed-out first-run screens lost the one-handed bottom bar.

Removing the auth gate raised the next question — *which surfaces* should have the bottom bar at all:

- **Native phone** — the bottom tab bar is the native idiom; clearly yes.
- **Web (phone browser / installed PWA)** — a `position: fixed` bottom bar **collides with the browser's
  own bottom chrome** (iOS Safari toolbar, Chrome address bar, the gesture / home-indicator area), so it
  double-stacks and is partially obscured. The web convention is a top bar + drawer/hamburger.
- **iPad** — bottom tabs are acceptable on iPadOS (Apple's App Store / Music use them), but the device is
  wide enough for a roomy persistent drawer, and the layout already flips to that drawer in landscape at
  the `Md` width breakpoint.

**On which heads, and at which widths, should the bottom bar be the primary navigation?**

## Decision drivers

- **Native-app feel** — a phone app wants a bottom tab bar; its one-handed reach is the point of
  ADR-0054's lever, including on account-free first-run screens.
- **Consistency within a platform** — no layout jump on sign-in; the same chrome signed in or out.
- **Respect the host** — on the web, don't fight the browser's own bottom chrome.
- **Use the width on a tablet** — iPad landscape has room for the persistent drawer; portrait is narrow
  and app-like.
- **Avoid a degenerate bar** — ADR-0054's concern about a lone full-width tab still holds.
- **Keep the detection minimal** — the shared UI already uses `OperatingSystem.IsBrowser()` as its
  web-vs-native signal (StorageModePicker, Groups, Settings); prefer that over a new form-factor seam.

## Considered options

1. **Keep the auth-gated bar** (status quo) — bar only for signed-in linked members, on any narrow
   viewport including web.
2. **Always-on, viewport-only** — bar on every narrow viewport regardless of head (the first same-day
   draft of this ADR). Simple, but puts the bar on mobile web where it collides with browser chrome.
3. **Native-only, width-based within native (chosen)** — bar on narrow viewports **in the native app**
   (phone, iPad portrait); the web uses the drawer at all widths; iPad landscape (wide native) uses the
   persistent drawer via the existing breakpoint.
4. **Native phone only** — exclude tablets too, via a `DeviceInfo.Idiom` form-factor seam, so iPad uses
   the drawer in both orientations.

## Decision outcome

**Chosen: option 3.** The bottom bar is gated on `!OperatingSystem.IsBrowser()` **and** the existing
`MudHidden Breakpoint="MdAndUp"` width gate (plus `_bottomNavEnabled` driving the app-bar hamburger and
the content's clearance padding). The net behaviour:

| Head / viewport | Primary navigation |
|---|---|
| Native phone (narrow) | **Bottom bar** |
| Native iPad portrait (narrow) | **Bottom bar** |
| Native iPad landscape (wide) | Persistent drawer |
| Web (any width) | Drawer + hamburger |

The auth gate is gone: a native narrow viewport shows the bar **signed in or out**. ADR-0054's
"no degenerate one-item bar" is **satisfied, not overridden** — the minimum bar is **two** tabs (Groups +
"More"), and with `flex: 1` each takes half the width, reading as a deliberate balanced bar; the
signed-in personal tabs fill the middle, so signing in grows the bar rather than replacing the chrome.

Option 2 is rejected because a fixed bottom bar on mobile web collides with the browser's own bottom
chrome. Option 4 is rejected for now: it needs a `DeviceInfo.Idiom` seam for a modest gain, and iPad
portrait (narrow) reads well as an app-like bottom bar, while landscape already gets the drawer — the
width signal expresses the tablet split adequately without new plumbing. The `IsBrowser()` split is the
one distinction worth making, and the shared UI already makes it elsewhere.

This **amends ADR-0054's** navigation-cleanup lever (its specific "signed-out state has no bottom bar"
rule); the rest of ADR-0054 stands.

## Consequences

- **Positive:** consistent, one-handed primary navigation in the native app (phone + iPad portrait), no
  layout jump on sign-in, and the bottom bar's reach now covers the signed-out first-run screens.
- **Positive:** the web keeps the drawer/hamburger it works best with — no collision with browser chrome;
  iPad landscape keeps its roomy persistent drawer.
- **Positive:** simpler shell — the auth-based `_showBottomBar` is replaced by one head-level flag
  (`_bottomNavEnabled = !OperatingSystem.IsBrowser()`) plus the existing width breakpoint.
- **Negative / accepted:** iPad flips between the bottom bar (portrait) and the drawer (landscape); this
  mirrors the existing responsive-drawer breakpoint and is a reasonable orientation adaptation. A future
  `DeviceInfo.Idiom` seam could unify iPad on the drawer if that flip proves jarring (option 4).
- **Negative / accepted:** a signed-out bar has two tabs with interior spacing; benign (`flex:1` keeps
  them balanced), and it grows to three/four tabs once the user is a linked member.
- **Follow-ups:** device-verify the signed-out bar on iPhone and iPad portrait, the drawer on iPad
  landscape, and the drawer-only web across widths, in DE/EN/FR; update
  [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) W2 notes to this rule.
- **New risks:** none significant; the change is head- + viewport-gated and reversible.

## Pros and cons of the options

### 1. Keep the auth-gated bar
- 👍 Avoids the multi-tab-spacing question; status quo.
- 👎 Navigation shape changes on sign-in; signed-out first-run loses the bar; two layouts to reason about.

### 2. Always-on, viewport-only
- 👍 Simplest; one consistent rule across heads.
- 👎 Puts a fixed bottom bar on mobile web, where it collides with the browser's own bottom chrome.

### 3. Native-only, width-based within native (chosen)
- 👍 Native-app feel on phone + iPad portrait; web keeps its drawer; iPad landscape keeps the roomy
  drawer; one existing signal (`IsBrowser()`), no new seam; honours the no-degenerate-bar rule.
- 👎 iPad flips bar↔drawer by orientation.

### 4. Native phone only (form-factor seam)
- 👍 iPad is consistent (drawer in both orientations); treats the tablet as a small desktop.
- 👎 Needs a `DeviceInfo.Idiom` seam; loses the app-like bottom bar on iPad portrait.

## Links

- Amends [ADR-0054](0054-mobile-native-feel-strategy.md) (mobile native-feel strategy — the bottom-bar /
  navigation-cleanup lever)
- Related ADRs: [ADR-0040](0040-ui-component-library-mudblazor.md) (MudBlazor shell + bottom bar),
  [ADR-0013](0013-wasm-api-dev-wiring.md) (the web head is a separate, browser-hosted origin)
- Features: [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (W2 navigation)
