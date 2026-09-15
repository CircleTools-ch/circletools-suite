---
id: FEAT-014
title: Add an event to the device calendar
status: Draft
owner: Adrian
target-milestone: Mobile-native-feel (post-v1)
bounded-context: SecretSanta
related: [UC-SS-021, FR-044, ADR-0028, ADR-0053, ADR-0056, NFR-001, NFR-002, NFR-003, TP-014]
---

# FEAT-014 — Add an event to the device calendar

> Realises MNF-6 from the
> [mobile-native-feel cluster](../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02),
> promoted through the process ([UC-SS-021](../use-cases/secret-santa/UC-SS-021-add-occasion-to-calendar.md)
> → [FR-044](../requirements/functional/FR-044-add-event-to-device-calendar.md)). It needs **no new
> ADR** — the one architectural piece, a calendar platform seam, is an amendment to
> [ADR-0028](../adr/0028-browser-platform-adapters-block.md).

## Problem & goals

- **Problem:** an [Event](../glossary/event.md) already carries its practical
  [details](../glossary/event-details.md) — date, location, notes — but there is no way to get the
  occasion onto a personal calendar. Reviewers across DE/EN/FR asked to be *reminded*, and it is the
  givers, not only the organizer, who forget the hand-over.
- **Goals:** a one-tap **Add to calendar** that hands the occasion to the phone's **own** calendar UI,
  pre-filled, for the person to review and save — for the organizer *and* every giver, working offline and
  account-free, with the platform (not the app) creating the entry. The entry also carries a **deep link
  back into the app at that occasion**, so tapping the calendar entry reopens the event (all three storage
  modes).
- **Non-goals:**
  - No silent/programmatic calendar write and **no calendar permission** — the user always sees and
    confirms (a deliberate scope call, [ADR-0028 amendment](../adr/0028-browser-platform-adapters-block.md)).
  - No reminders/notifications — that is MNF-8, a separate, larger feature.
  - No two-way sync, no editing a previously-added entry, no recurrence.
  - No domain, contract, server, or migration change — this is a read-only, client-only feature.

## Requirements & use cases covered

| Item | Covered by this feature? | Notes |
|------|--------------------------|-------|
| [UC-SS-021](../use-cases/secret-santa/UC-SS-021-add-occasion-to-calendar.md) — add an occasion to my calendar | ✅ | Organizer + giver surfaces |
| [FR-044](../requirements/functional/FR-044-add-event-to-device-calendar.md) — add an event to the device calendar | ✅ | All-day entry; gated on a date; no assignment; system-UI |
| [ADR-0028](../adr/0028-browser-platform-adapters-block.md) — calendar seam (amendment) | ✅ | `ICalendarService`, per-head delivery |
| [ADR-0053](../adr/0053-backend-free-first-mobile-release.md) — App Links / deep linking | ✅ (extends) | Reuses the reveal App Link machinery for the in-entry deep link; no new ADR |
| [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md) — local-first/offline | ✅ | Built on-device from local data; works on the reveal page |
| [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) — assignment privacy | ✅ | Entry carries no giver→recipient info |
| [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md) — DE/EN/FR | ✅ | Label + hint localized; culture-formatted budget |

## Design overview

A small platform seam, **`ICalendarService`**, added under
[ADR-0028](../adr/0028-browser-platform-adapters-block.md) (its 2026-09-15 amendment), mirroring
`IShareService` exactly. The **iCalendar (`.ics`) `VEVENT` is generated in shared, head-agnostic code**;
only delivery to the OS calendar is per-head:

- **Web** — `BrowserCalendarService` hands the `.ics` to the OS (a data/blob URL the browser opens),
  host-local in `SecretSanta.WebApp/Interop` beside `BrowserShareService`.
- **Android** — `MauiCalendarService` fires `Intent.ACTION_INSERT` on `CalendarContract.Events` (the
  reliable native path; Android has no consistent `text/calendar` default handler), in `App/Platform`.
- **iOS** — writes the shared `.ics` to a temp file and opens it (Calendar add-event preview).
  **Implementation deferred with the iOS head** ([ADR-0031](../adr/0031-ios-build-host.md)); spec'd here.

An **Add to calendar** button appears wherever an occasion with a date is shown — the organizer's event
**Details** section and event hub, and the giver-facing **reveal** and **my-assignments** screens. The
button is hidden when the occasion has no date.

```mermaid
flowchart LR
    subgraph Shared[Shared Ui / feature code]
      B["Add to calendar button<br/>(Details · hub · reveal · my-assignments)"]
      G["IcsBuilder.Build(CalendarEvent)<br/>all-day VEVENT"]
    end
    B --> S{ICalendarService}
    G --> S
    S -->|Web| W["BrowserCalendarService<br/>open .ics via OS"]
    S -->|Android| A["MauiCalendarService<br/>ACTION_INSERT (pre-filled)"]
    S -->|iOS (deferred)| I["open .ics<br/>Calendar preview"]
    W --> Cal[(Phone calendar UI — user confirms & saves)]
    A --> Cal
    I --> Cal
```

The `CalendarEvent` value is head-agnostic: `Title`, `Date` (date-only), optional `Location`, optional
`Notes`, and a `Url` (the deep link, below). It is assembled once from the occasion's
[event details](../glossary/event-details.md) plus the [budget](../glossary/budget.md) range, and never
carries an [assignment](../glossary/assignment.md).

### Deep link back into the app

The entry carries a link back into the app **at that occasion**, so tapping the calendar entry reopens the
event. It is the app's own event route —
`{origin}/groups/{groupId}/events/{eventId}` (the `EventHub` route
`@page "/groups/{GroupId:guid}/events/{EventId:guid}"`) — built from the **same configured base URL reveal
links use** (`RevealLinkOptions.BaseUrl` / `RevealBaseUrl`). It is **mode-agnostic**: it works identically
for a local, a [shared-by-link](../glossary/shared-by-link-group.md), and an account group, because the
device that added the entry already holds that group, so opening it just re-navigates there.

The link carries **no secret** — only the opaque group/event GUIDs; opening it does nothing unless the
device already has that group, so there is **no [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
concern** (unlike a reveal link, this is not a capability, just a location). On **Android** it reuses the
existing App Link + `DeepLinkService` machinery ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)):
one more intent-filter path alongside the reveal `/r` (and join `/join`) prefixes in `MainActivity`, and a
`DeepLinkService` case that routes to the event — working **offline**, exactly like reveal. On the **web**
the link opens the WASM app at that route in the browser. On **iOS** it falls back to the browser/PWA until
deep links are wired there (MNF-10); iOS is not a v1 target anyway.

> **Origin/fingerprint caveat — this behaves exactly like reveal links today, it is not a new
> limitation.** The link points at whatever origin the build is configured with (a dev/LAN address now, the
> real domain once hosting lands — a build-time setting, no code change,
> [ADR-0053](../adr/0053-backend-free-first-mobile-release.md)). On Android it auto-opens the installed app
> *silently* only once `assetlinks.json` carries the real Play App Signing SHA-256; until then Android
> shows the app chooser — the same placeholder-fingerprint state reveal links already ship with.

## ICS payload spec

A single all-day `VEVENT`:

- `SUMMARY` — the event name (e.g. `Christmas 2026`).
- `DTSTART;VALUE=DATE` = the event date; `DTEND;VALUE=DATE` = the next day (iCalendar all-day events are
  half-open). The date is **date-only** (stored midnight UTC via `MudDatePicker`), so there is **no
  time-of-day and no timezone** to carry — this sidesteps the classic all-day timezone-shift bug.
- `LOCATION` — the event location, omitted when empty.
- `URL` — the deep link back into the app at the occasion,
  `{origin}/groups/{groupId}/events/{eventId}` (see *Deep link* above).
- `DESCRIPTION` — the free-text notes, then the budget range on its own line (e.g. `10–30 CHF`, using the
  same `BudgetDisplay.Format` shape as the reveal, omitted when no budget), then the **deep link** on its
  own line — because some calendars do not surface the `URL` property, the link is repeated here so it is
  always tappable. Never omitted entirely, since the deep link is always present.
- `UID` — a stable id derived from the group + event id, so re-adding updates rather than duplicates where
  the calendar honours `UID`.
- `PRODID`, `VERSION:2.0`, `CALSCALE:GREGORIAN`; text fields escaped per RFC 5545 (`,`, `;`, `\`,
  newlines) and lines folded at 75 octets.

No `ORGANIZER`/`ATTENDEE`, and **never** any assignment data. The `URL`/`DESCRIPTION` deep link carries
only opaque GUIDs, not a secret (it is a location, not a capability — see *Deep link* above).

## Domain model impact

- **None.** Read-only over the existing `Event` / `EventDetails`
  ([ADR-0042](../adr/0042-events-as-first-class-entities.md)). No aggregate, event, or glossary term
  added — "Event details" already names the payload.

## API / contract changes

- **None.** No endpoint or DTO; the `.ics` is generated client-side. The reveal payload
  ([ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md)) is unchanged — the date/location/notes it
  already carries are what the reveal-page button reads, so no link-size impact.
- The deep link reuses the **existing configured base URL** (`RevealLinkOptions.BaseUrl` / `RevealBaseUrl`)
  and the app's existing `EventHub` route — **no new config setting, route, or contract**.

## Data & migration considerations

- **Postgres / SQLite / sync:** none. No stored state, nothing to migrate.

## Localization impact

- New user-facing strings → **DE / EN / FR** (+ neutral): the **Add to calendar** label and any hint.
  Provisional: *Zum Kalender hinzufügen / Add to calendar / Ajouter au calendrier*.
- The event name and notes carry through as typed; the budget range formats for the active culture
  ([ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md)). The `.ics` structural fields
  are locale-neutral.

## Testing strategy

Paired plan: [TP-014](TP-014-add-event-to-calendar.md). Levels: **unit** for the `.ics` builder (all-day
VEVENT shape, escaping/folding, notes+budget in `DESCRIPTION`, budget omitted when unset, no assignment
ever present) and the "button gated on a date" logic; **bUnit** for the button appearing on Details and
reveal only when a date is set and calling the seam (a fake `ICalendarService`); **platform/E2E notes**
for the Android intent and the web `.ics` download (iOS deferred); **localization parity** for the new
strings.

## Rollout / deployment & feature flags

- No feature flag — additive and inert until tapped.
- **Platform matrix:**

  | Concern | Web | Android (now) | iOS (deferred) |
  |---|---|---|---|
  | Deliver `.ics` to calendar | open/download `.ics` | `ACTION_INSERT` pre-filled editor | open `.ics` (Calendar preview) |
  | Calendar permission | none | **none** (`ACTION_INSERT` needs none) | none |
  | Deep link (entry → app) | opens WASM route in browser | App Link intent-filter path + `DeepLinkService` case (offline) | browser/PWA fallback (MNF-10) |

- **Android manifest:** no *calendar* permission is added; the only manifest touch is **one more App Link
  intent-filter path** for the event route, beside the existing reveal `/r` (and join `/join`) filters in
  `MainActivity` — the same mechanism reveal links already use ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)).
- **iOS** is deferred with the iOS head ([ADR-0031](../adr/0031-ios-build-host.md), like MNF-10): the
  `.ics` and the deep link both fall back to the browser/PWA there.

## Risks & open questions

- **Android OEM variance** — a few OEM calendars pre-fill `ACTION_INSERT` imperfectly; the entry is still
  editable before save. Accepted; confirm on the Pixel during the device pass.
- **All-day date correctness** — the one real footgun is an off-by-one from timezone handling; using
  `VALUE=DATE` (no time/zone) avoids it. Covered by a unit test asserting the exact `DTSTART`/`DTEND`.
- **Deep-link origin/fingerprint** — the entry's deep link inherits the reveal-link origin/fingerprint
  behaviour exactly (dev/LAN → real domain at build time; Android chooser until `assetlinks.json` gets the
  real Play-signing SHA-256). Called out in the design above as a *known, shared* trait, not a new
  limitation.
- **Open:** whether re-adding should rely on `UID` de-duplication or always create a fresh entry —
  defaulting to a stable `UID`; revisit if a calendar mishandles it.

## Definition of Done

- [ ] Meets acceptance criteria of all covered requirements
- [ ] Builds clean under analyzers (warnings = errors)
- [ ] Tests per the test plan pass; coverage target met
- [ ] Architecture boundaries respected (arch tests green — seam in `Ui.Abstractions`, impls host/head-local)
- [ ] Docs, glossary, and API comments updated
- [ ] Localized strings provided (DE/EN/FR)
- [ ] Verified in a real browser **and** on the Pixel (per CLAUDE.md): button gated on a date; Android
      opens the pre-filled editor; web opens/downloads the `.ics`; reveal-page path works offline; the
      entry's deep link reopens the app at the occasion (Android App Link; web route)
- [ ] Traceability links closed
