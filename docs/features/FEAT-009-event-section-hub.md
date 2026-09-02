---
id: FEAT-009
title: Event section hub (mobile-native event navigation)
context: SecretSanta
status: In progress
related: [ADR-0055, ADR-0042, ADR-0040, FR-034, TP-009]
---

# FEAT-009 — Event section hub

## Summary

Replace the event-level horizontal **router-tab strip** (*Details / Wishlists / Draw / Share*) — which
overflowed into a clumsy horizontally-scrolling control on a phone — with a **section hub** that is the
event landing on all widths, per [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md). The hub
is a vertical list of the sections, each with an icon, a one-line **status**, and a chevron; each section
page gets a **back-to-overview** link. Amends [ADR-0042](../adr/0042-events-as-first-class-entities.md)'s
event navigation (presentation only — routes' semantics shift slightly, the domain is unchanged).

## Design

### Navigation & routes
- **`EventHub`** (`Pages/EventHub.razor`) takes the event landing route
  `/groups/{groupId}/events/{eventId}`: header (← group, event name) + a vertical list of four rows —
  **Details, Wishlists, Draw, Share** — each a tappable `MudPaper` (`ct-hub-row`, ≥48px, keyboard-
  operable) navigating to that section's route.
- **`EventEditor`** (Details) moves to its own route `/groups/{groupId}/events/{eventId}/details`; the
  `/wishlists`, `/draw`, `/share` routes are unchanged.
- **`EventBackNav`** (`Shared/EventBackNav.razor`) replaces the retired `EventNav` on every section page:
  a "← Overview" link back to the hub, and it records nav recency (as `EventNav` did).
- **`EventNav`** (the tab strip) is **removed**. `GroupNav` (group level: Events / Members / Exclusions)
  is unchanged — the group level keeps its tabs (fewer, shorter labels); a matching hub there is a
  possible later consistency step, not done here.

### Per-section status (from the one `GetEventQuery` the hub loads)
- **Details** — the event date, when set.
- **Wishlists** — "{filled} of {total}" members' wishlists filled (`HubWishlistCount`); hidden when there
  are no members.
- **Draw** — reuses `DrawCompleteBadge` ("Drawn") / `DrawInvalidatedBadge` ("Re-draw needed"), plus a new
  `HubDrawPending` ("Not drawn yet").
- **Share** — `HubShareReady` ("Ready") once a draw exists; nothing before.

### Localization
- Four new strings in all four resx (neutral + de-CH / en / fr-CH): `EventOverview`, `HubWishlistCount`,
  `HubDrawPending`, `HubShareReady`. Row labels reuse the existing `NavEventDetails` / `NavWishlists` /
  `NavDraw` / `NavShare`. Localization parity test green.

## Domain / contracts / data

- **None.** No aggregate, DTO, query, API, or sync change — the hub reads the existing `EventDto` via
  `GetEventQuery`.

## Testing strategy

Paired plan: [TP-009](TP-009-event-section-hub.md). Weighted to on-screen verification per `CLAUDE.md`
(this is a navigation/layout change). **Browser-verified** (Playwright, 390px, en-US): the hub is the
event landing, **no horizontal tab strip**, four section rows render with the right labels/status,
tapping a row navigates to its route and shows the back-to-overview link, and the link returns to the
hub; console clean. **On-device (Pixel) pass** pending; EN and DE both want a look (FR by parity).

## Risks & open questions

- A **wide user loses one-click tab switching** between sections (returns to the hub instead) — an
  accepted trade recorded in [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md); reversible by
  layering a wide-only tab bar back on without changing routes.
- **Group-level consistency** (keep `GroupNav` tabs vs a matching hub) is deferred.

## Traceability

- ADRs: [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md) (this decision),
  [ADR-0042](../adr/0042-events-as-first-class-entities.md) (event nav it amends),
  [ADR-0040](../adr/0040-ui-component-library-mudblazor.md) (MudBlazor nav)
- Requirement: [FR-034](../requirements/functional/FR-034-multiple-events-per-group.md)
- Test plan: [TP-009](TP-009-event-section-hub.md)
- Backlog: [MCN-2](../roadmap/backlog.md#mobile-chrome--navigation-review--secret-santa-2026-09-02)
