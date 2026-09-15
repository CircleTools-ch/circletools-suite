---
id: FR-046
title: Device-local occasion reminders
type: functional
status: Implemented
priority: Could
source: UC-SS-023
verification: test
related: [UC-SS-023, FEAT-016, TP-016, ADR-0060, ADR-0028, ADR-0053, ADR-0054, ADR-0031, ADR-0056, NFR-001, NFR-002, NFR-003]
---

# FR-046 — Device-local occasion reminders

## Description

The app offers **device-local reminders** about a user's own [occasions](../../glossary/event.md),
delivered as OS notifications scheduled and fired **on the device** from local data — no account, no
connection, no backend. There are **three kinds**, each independently switchable and **off by default**:

| Kind | Who | Fires | Default lead time |
|------|-----|-------|-------------------|
| **Draw not done yet** | the occasion's organizer | before a dated occasion whose draw is not yet run | **3 days** before |
| **Event is tomorrow** | anyone holding the occasion locally | before a dated occasion | **1 day** before |
| **My wishlist is empty** | the user, about their own wishlist | before a dated occasion where their own wishlist is empty | **3 days** before |

The controls live in a **Notifications** section on the [Settings](#dependencies--related) page as a
**global per-kind on/off** toggle (applying to all the user's occasions). The **OS notification
permission** is requested the **first time** the user enables any kind — not on app start.

Reminders are **scheduled, rescheduled and cancelled** from local occasion state so they never fire
wrongly or twice: setting/changing an occasion's date (re)schedules; running the draw cancels that
occasion's draw reminder; filling the wishlist cancels its empty-wishlist reminder; deleting the occasion
cancels all of its reminders; app start reconciles the schedule. Times already in the past are not
scheduled.

A reminder body carries **only** the group/occasion name and the reason. It **never** contains an
[assignment](../../glossary/assignment.md) or any who-gives-to-whom information
([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).

**Scope (v1):** device-local only. Reminding **another** person (nudge a co-member's empty wishlist,
"remind everyone") is **out of scope** — it needs a reachable origin and push backend and is parked with
the hosting decision ([ADR-0053](../../adr/0053-backend-free-first-mobile-release.md),
[ADR-0060](../../adr/0060-local-notification-scheduling-and-permission.md)).

**Platforms:** Android now; **web is a no-op** (the browser/PWA cannot reliably schedule while closed —
the toggles render but say reminders need the installed app); **iOS deferred** with the iOS head
([ADR-0031](../../adr/0031-ios-build-host.md)). This is phone-first, mirroring the haptics seam.

## Rationale

Being nudged at the right moment — run the draw, the exchange is tomorrow, fill your wishlist — is the
single strongest reason to *install* the app rather than bookmark a page, and the cross-market
[competitor scan](../../brainstorming/2026-09-02-secret-santa-competitor-research.md) found reviewers
explicitly asking to be nudged to fill their wishlist. Doing it **device-local and opt-in** delivers that
value now, without waiting on hosting and without weakening the account-free/offline promise or the
assignment-privacy guarantee.

## Acceptance criteria

```gherkin
Given a fresh install
When I open Settings › Notifications
Then all three reminder kinds are shown and all are off

Given all reminder kinds are off and I have never granted notification permission
When I switch on my first reminder kind
Then the app requests the OS notification permission

Given "Event is tomorrow" is on and permission is granted
And a locally-known occasion dated tomorrow
When the scheduled time is reached
Then the device shows a notification naming the group/occasion and that it is tomorrow
And the notification body contains no assignment information

Given "Draw not done yet" is on for an occasion I organize whose draw is not run
When I then run the draw
Then that occasion's draw reminder is cancelled and does not fire

Given "My wishlist is empty" is on for an occasion where my wishlist is empty
When I add an item to my wishlist
Then that occasion's empty-wishlist reminder is cancelled

Given reminders are scheduled for an occasion
When I change the occasion's date
Then the reminders are rescheduled relative to the new date
And when I delete the occasion, all of its reminders are cancelled

Given I decline the OS notification permission
When a scheduled time is reached
Then no notification is shown and nothing else in the app changes

Given I am on the web head
When I open Settings › Notifications
Then it explains that reminders need the installed app (no notification is scheduled)
```

## Dependencies & related

- Derived from: [UC-SS-023](../../use-cases/secret-santa/UC-SS-023-be-reminded-about-my-occasions.md)
- Implemented by: [FEAT-016](../../features/FEAT-016-device-local-occasion-reminders.md) /
  [TP-016](../../features/TP-016-device-local-occasion-reminders.md)
- Governing ADR: [ADR-0060](../../adr/0060-local-notification-scheduling-and-permission.md) — the
  notification scheduling/permission/seam strategy
- Related: [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) (platform seams),
  [ADR-0053](../../adr/0053-backend-free-first-mobile-release.md) (backend-free; the origin gate that keeps
  cross-member push out of v1), [ADR-0054](../../adr/0054-mobile-native-feel-strategy.md) (native feel via
  OS integration), [ADR-0031](../../adr/0031-ios-build-host.md) (iOS deferred),
  [ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md) (date formatting),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md) (offline/on-device),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (no assignment in a reminder),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md) (DE/EN/FR)
- Surface: the [Settings](../../use-cases/secret-santa/UC-SS-023-be-reminded-about-my-occasions.md) page
  (MCN-3), which already gathers appearance/language/version.
- Glossary: [Event](../../glossary/event.md), [Wishlist](../../glossary/wishlist.md),
  [Assignment](../../glossary/assignment.md)
