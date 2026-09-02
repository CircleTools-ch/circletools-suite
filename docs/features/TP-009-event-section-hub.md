---
id: TP-009
title: Test plan — Event section hub
context: SecretSanta
status: Draft
related: [FEAT-009, ADR-0055]
---

# TP-009 — Test plan — Event section hub

Covers [FEAT-009](FEAT-009-event-section-hub.md) under
[ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md). This is a **navigation/layout** change,
so the weight is on **on-screen verification** per `CLAUDE.md`, not unit tests — the load-bearing
properties are all visual/behavioural:

- The event landing is the **hub** (not the old Details-with-tabs), on every width.
- There is **no horizontal tab strip** anywhere at the event level.
- Each **section row** navigates to its route, and each section page returns to the hub via
  **back-to-overview**.
- The per-section **status** reads correctly and updates with the data (draw done, wishlists filled).

## Planned automated coverage (bUnit — cheap checks)

| Area | Where (planned) | What it asserts |
|------|-----------------|-----------------|
| Hub renders the four rows | `EventHubTests` | Details / Wishlists / Draw / Share rows present; no `.mud-tabs` |
| Draw status | `EventHubTests` (fake `ISender`) | "Not drawn yet" with no draw; "Drawn" with a draw; "Re-draw needed" when invalidated |
| Wishlist status | `EventHubTests` | "{filled} of {total}" from the event's wishlists; hidden when no members |
| Row navigation | `EventHubTests` | clicking a row navigates to the matching `/details` / `/wishlists` / `/draw` / `/share` route |
| Localization parity | existing `LocalizationParityTests` | the four new keys exist in all cultures (already green) |

Naming is indicative; the bar is that these exist and are green **and** the full existing suite stays
green (currently 29/29).

## On-screen verification (the primary sign-off)

**Browser (done — Playwright, 390px, en-US):** created a group → added an event → landed on the **hub**;
asserted **no tab strip**, four rows (Details / Wishlists / Draw "Not drawn yet" / Share links), tapping
**Draw** navigates to `/draw` and shows the **Overview** back-link, and the link returns to the hub;
console clean.

**On device (Pixel) — pending:** walk an event on the phone in **DE** (and glance at **EN**), FR by
parity: open an event → the hub reads cleanly with no horizontal scroll; the status rows are truthful
(fill some wishlists → "x von y" moves; run the draw → "Ausgelost", Share → "Bereit"); back-to-overview
feels right; console clean.

## Exit criteria

- [ ] Planned bUnit checks exist and are green; full suite stays green (no regression)
- [ ] **On-device (Pixel) pass** done (DE + EN), console clean — the primary sign-off
- [ ] No horizontal event tab strip remains; hub is the landing on all widths
- [ ] Per-section status verified truthful against real data (draw, wishlists)
- [ ] Traceability closed (FEAT-009 ↔ TP-009 ↔ ADR-0055)
