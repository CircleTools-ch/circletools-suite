---
status: Accepted
date: 2026-09-02
deciders: [Adrian]
tags: [ui, ux, mobile, navigation, secret-santa]
---

# ADR-0055 — Event-level navigation on mobile: a section hub, not router-tabs

## Context and problem statement

[ADR-0042](0042-events-as-first-class-entities.md) introduced two-level navigation for a Secret-Santa
group, and [ADR-0040](0040-ui-component-library-mudblazor.md) realised it with MudBlazor:

- **Group level** (`/groups/{id}`) — a `MudTabs` strip: *Events / Members / Exclusions* (`GroupNav`).
- **Event level** (`/groups/{id}/events/{eventId}/…`) — a `MudTabs` strip: *Details / Wishlists / Draw /
  Share* (`EventNav`).

In both, the tabs carry **no panel content** — each is a router: changing tab calls
`Navigation.NavigateTo` and the section renders as its own route below. Driving the app on a phone
([mobile-first is the stated priority](0054-mobile-native-feel-strategy.md)) surfaced that the **event
strip is clumsy on mobile**:

- Four sections with long German/French labels (*Wunschliste*, *Auslosung*) do not fit a narrow phone,
  so `MudTabs` falls back to a **horizontally scrollable** mid-page strip — a control that is neither a
  top-tab bar nor a bottom nav, and whose off-screen tabs are easy to miss.
- Using a tabs component purely as a router (empty panels) is a web idiom, not a native mobile one.
- It carries **no sense of the workflow** an event actually has — fill wishlists → run the draw → share
  the links — so nothing guides the organiser through it.

The product is **one shared Razor UI** rendered by both the WASM web heads and the MAUI Blazor Hybrid
heads ([ADR-0025](0025-shared-ui-building-blocks.md)), so this is a suite-wide UI decision, not a
device-only tweak.

**How should an event's sections be navigated so the phone experience is native and the workflow is
clear, without forking the shared UI or abandoning the neutral Material vocabulary?**

## Decision drivers

- **Mobile-first** ([ADR-0054](0054-mobile-native-feel-strategy.md)) — the phone gets the most attention;
  "native feel via ergonomics", not a scrolling mid-page tab strip.
- **One shared component set** on web + MAUI ([ADR-0025](0025-shared-ui-building-blocks.md)).
- **The event workflow is inherently sequential/hub-shaped** — details, then wishlists, then the draw,
  then sharing — and per-section **status** (drawn? how many wishlists filled?) is meaningful.
- **Long DE/FR labels** must not overflow (NFR-003).
- Stay within the **neutral Material** look ([ADR-0040](0040-ui-component-library-mudblazor.md)); no
  Cupertino/adaptive fork ([ADR-0054](0054-mobile-native-feel-strategy.md) declines that).
- Wide viewports already work fine with the horizontal tabs; don't regress them.

## Considered options

1. **Keep the horizontal router-tabs** (status quo) on all widths.
2. **A fixed segmented control** (no horizontal scroll) for the four sections.
3. **An event "section hub"** — the event landing screen becomes a **vertical list of the sections** as
   tappable rows, each with an icon, label, a short **status**, and a chevron; tapping opens that
   section's route, with a back affordance to the hub. Applied **responsively**: the hub on narrow
   viewports, the existing horizontal tabs retained on wide.
4. **An event-scoped bottom tab bar** — a second bottom navigation while inside an event.

## Decision outcome

**Chosen: option 3 — an event section hub, made the event landing on all viewport widths, retiring the
horizontal tab strip.**

- The event route `/groups/{id}/events/{eventId}` becomes the **hub**: the event header (group ← back,
  event name) followed by a vertical list — **Details, Wunschliste, Auslosung, Teilen** — each row an
  icon + label + a one-line **status** + chevron, with a ≥48px hit area
  ([FEAT-008 W1](../features/FEAT-008-mobile-native-feel-polish.md)). Tapping a row navigates to that
  section's route.
- **Details gets its own route** `/groups/{id}/events/{eventId}/details` (it was the old landing); the
  other section routes (`/wishlists`, `/draw`, `/share`) are unchanged. Each section screen shows a
  **back-to-overview** link to the hub in place of the old tab strip.
- **Status per row** (best-effort, read from the one `GetEventQuery` the hub already loads): *Draw* shows
  "Drawn" / "Re-draw needed" / "Not drawn yet"; *Wishlists* shows "3 of 5"; *Share* shows "Ready" once
  drawn; *Details* shows the date when set. This is what turns a flat menu into workflow guidance.

**Why the hub on all widths (a refinement made during implementation), not "tabs on wide".** The initial
draft kept the tabs on wide and showed the hub only on narrow. Building it revealed that this forces the
**landing route to render two entirely different screens by width** (hub vs Details-with-tabs), which a
CSS breakpoint (`MudHidden`) cannot express as a *route* — it would need the Details editor rendered a
second time inside the landing and a duplicate Details route, for little gain. Making the hub the landing
on every width is one screen per route, one code path, and no per-platform fork; the status-rich hub also
reads fine as a desktop landing. The accepted cost is that a **wide user loses one-click tab switching**
between sections (they return to the hub, one click away, to switch) — acceptable for a mobile-first
family app, and reversible later (a wide-only tab bar could be layered back on without changing routes).

Option 3 over the others: option 1 is the clumsiness being fixed; option 2 still cannot fit four long
DE/FR labels on a narrow phone and carries no status; option 4 nests a second bottom bar under the app's
top-level one ([ADR-0040](0040-ui-component-library-mudblazor.md)), which is confusing and steals the
scarce bottom edge. The hub is the standard native pattern for "a thing with several sections and a
workflow".

**Scope.** This decision covers the **event level** (`EventNav`). The **group level** (`GroupNav`) is
addressed in the amendment below. This ADR **amends** ADR-0042's presentation of event navigation; the
domain model and routes are unchanged.

## Amendment (2026-09-02) — the group level gets the same hub

The group-level tab strip (`GroupNav`: *Events / Members / Exclusions*) is retired the same way, for the
same reasons and by the same maintainer request:

- The **group landing** `/groups/{groupId}` (was `GroupEditor` with tabs) becomes the **group hub**: the
  **occasions (events) list** is the primary content — the full list plus the add-event control — and
  **Members** and **Exclusions** sit **below it as section rows** (icon + count + chevron, the same
  `ct-hub-row` as the event hub). `GroupNav` is removed; Members/Exclusions pages get a `GroupBackNav`
  "← group name" link back to the hub.
- **The group-rename field leaves the landing.** An always-visible name text box made the hub lead with a
  form; the rename now lives behind the header's **overflow menu → "Rename"**, which opens a bottom sheet
  (the FEAT-008 W4 `TextInputSheet`, extended to accept the current name as its initial value). The label
  is **"Rename"** (`Umbenennen` / `Renommer`) — accurate while the name is the only group-level setting;
  if more settings arrive, promote it to a **"Group settings"** screen.
- Unlike the event hub, the group hub keeps the existing **inline event cards** (open / rename / remove an
  occasion) — those are the list, not a menu.

Same trade as the event level (a wide user returns to the hub to switch sections); same "one screen per
route" simplicity. Realised in [FEAT-009](../features/FEAT-009-event-section-hub.md).

## Consequences

- **Positive:** a thumb-friendly, no-horizontal-scroll event screen that reads as native and **guides the
  workflow** via per-section status; long DE/FR labels are no longer a layout hazard; wide screens are
  unchanged.
- **Negative / accepted:** the event **landing screen changes meaning** (hub instead of Details), Details
  gains its own route, and a **wide user loses one-click tab switching** between sections (the hub is one
  click away). Per-section status needs a small read model (all from the one `GetEventQuery` the hub loads).
- **Neutral:** routes and the domain are untouched; deep links to a section still work; the hub is just a
  new view over the same navigation.
- **Follow-ups:**
  1. Write a **feature spec** (FEAT) for the event hub — the row set, the status per row and its strings
     (DE/EN/FR + neutral, [ADR-0032](0032-ui-tone-of-voice-and-gender-neutral-terms.md)), the back-to-hub
     affordance, and the wide/narrow split.
  2. Decide **group-level consistency** (keep `GroupNav` tabs vs a matching hub).
  3. **Verify on a device** (the CLAUDE.md rule), including the workflow reading in all three languages.
- **New risks:** low — worst case the hub reads as an extra tap before a section; mitigated by showing
  status on the hub so the extra screen carries information, not just links.

## Pros and cons of the options

### 1. Horizontal router-tabs (status quo)
- 👍 Already built; fine on wide screens; deep-link friendly.
- 👎 Overflows to a horizontally scrolling mid-page strip on narrow; off-screen tabs missed; no workflow cue.

### 2. Fixed segmented control
- 👍 No horizontal scroll; compact.
- 👎 Four long DE/FR labels still do not fit a narrow phone; no room for status.

### 3. Event section hub as the landing on all widths (chosen)
- 👍 Native, thumb-friendly, no overflow; per-section status guides the workflow; one shared component and
  one screen per route (no per-width branching); reads fine as a desktop landing too.
- 👎 New landing-screen semantics (Details gains its own route); a wide user returns to the hub to switch
  sections instead of a one-click tab; needs a small status read model.

### 4. Event-scoped bottom tab bar
- 👍 Very native for a fixed small set.
- 👎 Nests a second bottom bar under the app's top-level one (ADR-0040) — confusing; consumes the bottom edge.

## Links

- Amends: [ADR-0042](0042-events-as-first-class-entities.md) (event navigation presentation)
- Related ADRs: [ADR-0040](0040-ui-component-library-mudblazor.md) (MudBlazor nav),
  [ADR-0054](0054-mobile-native-feel-strategy.md) (mobile-first strategy),
  [ADR-0025](0025-shared-ui-building-blocks.md) (one shared UI),
  [ADR-0032](0032-ui-tone-of-voice-and-gender-neutral-terms.md) (UI copy)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md)
- Requirements: [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Backlog: [Mobile native-feel pass](../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02)
