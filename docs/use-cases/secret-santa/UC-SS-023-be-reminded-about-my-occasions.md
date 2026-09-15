---
id: UC-SS-023
title: Be reminded about my occasions
app: Secret-Santa
actors: [Organizer, Participant]
status: Approved
priority: Could
related: [FR-046, FEAT-016, TP-016, ADR-0060, ADR-0028, ADR-0053, ADR-0054, ADR-0031, NFR-001, NFR-002, NFR-003]
---

# UC-SS-023 — Be reminded about my occasions

## Summary / goal

A user wants the app to **remind them, on their own device, at the right moment** about a
[Secret-Santa occasion](../../glossary/event.md) — that the draw still has to be run, that the exchange is
tomorrow, or that their own [wishlist](../../glossary/wishlist.md) is still empty — so they act in time
without having to remember to open the app. Reminders are **opt-in per kind** and fire **entirely on the
device** from local occasion data; no account, connection, or backend is involved.

## Primary actor & stakeholders

- **Primary actor:** the **Organizer** (for the draw reminder) and any **Participant** (for
  event-tomorrow and their own empty-wishlist), each on their **own device**.
- **Stakeholders & interests:**
  - *The user* — wants a nudge at the useful moment, on their terms, and no noise otherwise.
  - *The other participants* — indirectly benefit (a draw that actually happens, wishlists that get
    filled), but are **never** the recipients of a reminder here — a user is reminded only about
    themselves and their own occasions (device-local; cross-person nudging is out of scope, see
    [FR-046](../../requirements/functional/FR-046-device-local-occasion-reminders.md) and
    [ADR-0060](../../adr/0060-local-notification-scheduling-and-permission.md)).

## Preconditions

- The occasion (and, for the draw/tomorrow reminders, its **date**) exists in the app's local store on
  this device — created here, synced from the user's account, or joined by link
  ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)).
- The user has **switched the relevant reminder kind on** in Settings (all kinds are **off by default**).
- The device has granted the OS notification permission (requested the first time the user enables any
  reminder — see the main scenario).

## Postconditions

- **Success:** at the computed time, the device shows a local notification whose body names only the
  occasion and the reason (draw due / tomorrow / empty wishlist) — **never** an
  [assignment](../../glossary/assignment.md). Tapping it opens the app at the relevant place.
- **Failure:** no notification is shown (permission denied, reminder kind off, the underlying condition no
  longer holds, or the platform has no local-notification support — e.g. the browser). Nothing else in the
  app changes; the occasion and its data are untouched.

## Trigger

The user turns on a reminder kind in Settings; thereafter the app schedules and fires the reminders itself
as occasion data and the clock dictate.

## Main success scenario

1. The user opens **Settings** and finds the **Notifications** section listing the reminder kinds:
   *Draw not done yet*, *Event is tomorrow*, *My wishlist is empty* — all **off**.
2. The user switches on **Event is tomorrow**.
3. Because this is the first reminder enabled, the app **requests the OS notification permission**; the
   user grants it.
4. The app **schedules** a device-local notification for every locally-known occasion that has a date,
   one day before it (past-due times are not scheduled).
5. The evening before an occasion, the device shows *"[Group] — the exchange is tomorrow."*
6. The user taps it and the app opens at that occasion.

## Extensions / alternate flows

- **2a.** The user enables **Draw not done yet** → the app schedules, for each dated occasion the user
  **organizes** whose draw is not yet run, a notification the configured lead time before the date
  (default **3 days**). Running the draw **cancels** that occasion's draw reminder.
- **2b.** The user enables **My wishlist is empty** → the app schedules, for each dated occasion where the
  user's own wishlist is empty, a notification the configured lead time before (default **3 days**).
  Filling the wishlist **cancels** it.
- **4a.** An occasion's **date changes** → its reminders are **rescheduled**; an occasion is **deleted**
  → its reminders are **cancelled** — so there are never stale or duplicate reminders.
- **3a.** The user **declines** the permission → no reminders fire; the toggles still reflect the user's
  choice, and the app surfaces a one-line note that notifications are off at the OS level and how to
  re-enable them. Turning a kind on later re-requests where the platform allows.
- **1a.** On the **web** head the Notifications section renders but says reminders **need the installed
  app** — the browser cannot reliably schedule while closed (phone-first, like haptics; see
  [ADR-0060](../../adr/0060-local-notification-scheduling-and-permission.md)).

## Exceptions

- **The platform cannot schedule a local notification** (web today, or a device that denies it) → the
  feature is a silent no-op; enabling a toggle records the preference but nothing fires. No error is shown
  beyond the informational note above.
- **The occasion condition changed before the reminder fires** (draw was run, wishlist filled, occasion
  deleted, date moved into the past) → the reminder is cancelled/rescheduled so it does not fire wrongly.

## Non-functional notes

- **Offline/local-first:** reminders are scheduled and delivered **entirely on the device** from local
  data — no account, network, or backend ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** every reminder title/body and the Settings labels are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); the occasion date
  formats for the active culture ([ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md)).
- **Privacy:** a notification body carries only the group/occasion name and the reason — **never** who
  drew whom ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). No personal data
  leaves the device.
- **Performance:** scheduling is a cheap, bounded pass over the user's local occasions on the relevant
  triggers; nothing runs continuously.

## Open questions

- Whether the default lead times (3 days / 1 day / 3 days) should later be user-adjustable — deferred; v1
  ships the defaults with per-kind on/off only (see
  [FEAT-016](../../features/FEAT-016-device-local-occasion-reminders.md)).

## Traceability

- Requirements: [FR-046](../../requirements/functional/FR-046-device-local-occasion-reminders.md);
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADR: [ADR-0060](../../adr/0060-local-notification-scheduling-and-permission.md); relates to
  [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) (platform seams),
  [ADR-0053](../../adr/0053-backend-free-first-mobile-release.md) (backend-free / origin gate),
  [ADR-0054](../../adr/0054-mobile-native-feel-strategy.md) (native feel via OS integration),
  [ADR-0031](../../adr/0031-ios-build-host.md) (iOS deferred)
- Feature spec: [FEAT-016](../../features/FEAT-016-device-local-occasion-reminders.md) /
  [TP-016](../../features/TP-016-device-local-occasion-reminders.md)
- Source: **MNF-8** in the
  [mobile-native-feel cluster](../../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02),
  from the [mobile brainstorm (2026-09-01)](../../brainstorming/2026-09-01-secret-santa-mobile.md)
- Glossary terms: [Event](../../glossary/event.md), [Wishlist](../../glossary/wishlist.md),
  [Assignment](../../glossary/assignment.md), [Group](../../glossary/group.md)
