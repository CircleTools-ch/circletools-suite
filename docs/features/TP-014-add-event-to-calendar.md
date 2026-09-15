---
id: TP-014
feature: FEAT-014
status: Draft
related: [UC-SS-021, FR-044, ADR-0028, ADR-0053, NFR-001, NFR-002, NFR-003]
---

# TP-014 — Test plan for FEAT-014 (Add an event to the device calendar)

## Scope & objectives

Verify that [FEAT-014](FEAT-014-add-event-to-calendar.md) turns an occasion with a date into a correct,
all-day calendar entry through the platform's own confirm UI, on the organizer and giver surfaces, offline
and account-free — and that it never leaks an [assignment](../glossary/assignment.md). The weight is on the
**`.ics` builder** (the one piece with real logic and a classic all-day/timezone footgun), the
**deep-link URL** it embeds (correct route from the configured base URL, present in both `URL` and
`DESCRIPTION`, carrying no secret), and the **gating** (no date → no button; no assignment ever in the
payload), because everything else is a thin per-head hand-off to the OS. The Android deep-link *routing*
(`DeepLinkService` → `EventHub`) is unit-testable; auto-open is confirmed on device.

## Test levels

- **Unit** — the `.ics` `VEVENT` builder and the "has a date" gate (no I/O).
- **UI/component** — bUnit over the button on the Details and reveal surfaces with a fake `ICalendarService`.
- **Architecture** — the seam sits in `Ui.Abstractions`; implementations are host/head-local (existing
  `SharedUiBlockLayeringTests`).
- **End-to-end / platform** — Android intent and web `.ics` open, driven where automatable; iOS deferred.
- **Exploratory** — on-device (Pixel) pass: pre-fill fidelity across the default and Google calendars.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-044 / all-day shape | Builder emits `DTSTART;VALUE=DATE` = event date and `DTEND;VALUE=DATE` = next day; no time/zone | Unit | ✅ |
| FR-044 / date correctness | An event dated 2026-12-24 yields `DTSTART:20261224` regardless of machine timezone (no off-by-one) | Unit | ✅ |
| FR-044 / title | `SUMMARY` = event name; RFC-5545 escaping of `,` `;` `\` and newlines; 75-octet line folding | Unit | ✅ |
| FR-044 / location | `LOCATION` present when set, omitted when empty | Unit | ✅ |
| FR-044 / notes + budget | `DESCRIPTION` = notes then budget range (e.g. `10–30 CHF`); budget omitted when unset; whole field omitted when both empty | Unit | ✅ |
| NFR-002 / no assignment | Builder output for a drawn event contains no recipient/giver data (property/assertion) | Unit | ✅ |
| FR-044 / deep link in URL | For group id "G" / event id "E", the `URL` property = `{base}/groups/G/events/E` using `RevealLinkOptions.BaseUrl` | Unit | ✅ |
| FR-044 / deep link in DESCRIPTION | The same deep link is also present in `DESCRIPTION` (calendars that ignore `URL` still show it) | Unit | ✅ |
| NFR-002 / link is a location | The deep link contains only opaque GUIDs — no token/secret/assignment | Unit | ✅ |
| FR-044 / gate present | Details section shows "Add to calendar" when the event has a date | UI (bUnit) | ✅ |
| FR-044 / gate absent | No "Add to calendar" when the event has no date (any surface) | UI (bUnit) | ✅ |
| FR-044 / giver surface | Reveal page shows the action for a dated occasion and calls the seam with the built event | UI (bUnit) | ✅ |
| FR-044 / invokes seam | Tapping the button calls `ICalendarService` once with the expected `CalendarEvent` (fake seam) | UI (bUnit) | ✅ |
| NFR-001 / offline | Reveal-page path builds the entry with no network/account (component test with no API) | UI (bUnit) | ✅ |
| FR-044 / web delivery | Browser build opens/downloads a valid `.ics` (MIME `text/calendar`) | E2E (web) | ⚠️ where automatable |
| FR-044 / Android delivery | `ACTION_INSERT` opens the pre-filled new-event editor with title/date/location/notes | E2E (device) | ❌ manual (Pixel) |
| FR-044 / no permission | No calendar permission prompt; no manifest calendar permission added | Review + device | ❌ manual |
| FR-044 / Android deep link | The event route is registered as an App Link intent-filter path (beside reveal `/r`, join `/join`) and `DeepLinkService` routes `/groups/{gid}/events/{eid}` to `EventHub` | Unit + device | ⚠️ routing unit-testable; auto-open manual (Pixel, needs real `assetlinks.json` fingerprint) |
| FR-044 / web deep link | The link opens the WASM app at `/groups/{gid}/events/{eid}` in the browser | E2E (web) | ⚠️ where automatable |
| FR-044 / mode-agnostic link | The deep link is identical and resolves for a local, shared-by-link, and account group | Unit | ✅ |
| NFR-003 / parity | "Add to calendar" label + hint present and correct in DE/EN/FR (+ neutral); parity test | Unit/Review | ✅ |
| iOS | `.ics` opens Calendar add-event preview; deep link falls back to browser/PWA | — | Deferred with the iOS head |

## Test data & environments

- Fixtures: an event **with** a full detail set (date + location + notes + min/max budget), one with a
  **date only**, one with **no date**, and a **drawn** event (to assert no assignment reaches the payload).
- Budget cases: min–max range, max-only, and none — to exercise the `DESCRIPTION` budget line and its
  omission.
- Environments: unit/bUnit in the standard test host; web E2E via the browser-driving host
  ([ADR-0045](../adr/0045-browser-driving-e2e-test-host.md)); Android on the Pixel per
  [device testing](../deployment/device-testing.md).

## Localization & accessibility notes

- DE/EN/FR (+ neutral) for the button label and any hint; budget range formats for the active culture
  ([ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md)).
- The button has an accessible name and a ≥48 px touch target (inherits the FEAT-008 mobile density
  baseline).

## Exit criteria

- [ ] All acceptance criteria covered by automated tests where a headless environment allows (builder,
      gating, bUnit, parity); the Android device path and no-permission check confirmed manually on the Pixel
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
- [ ] Verified in a real browser and on the Pixel per CLAUDE.md (not on a green suite alone)
