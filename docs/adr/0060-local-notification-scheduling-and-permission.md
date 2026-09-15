---
status: Accepted
date: 2026-09-15
deciders: [adrian-moll]
tags: [architecture, mobile, notifications, platform-seam, privacy, secret-santa]
---

# ADR-0060 — Device-local notification scheduling, permission & platform seam

## Context and problem statement

MNF-8 (the [mobile-native-feel cluster](../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02),
from the [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)) is the single strongest
reason to *install* the Secret-Santa app rather than bookmark it: **reminders** — run the draw before the
event, the exchange is tomorrow, your wishlist is still empty. Realising it forces several coupled
decisions the app has not made before, because it has never scheduled anything to happen while it is
closed:

- **Reach.** "Remind *everyone*" or "nudge a co-member whose wishlist is empty" means delivering to
  **someone else's device**, which needs a reachable production origin and a push backend — both deferred
  ([ADR-0053](0053-backend-free-first-mobile-release.md)). "Remind *me* about *my* occasion" needs neither.
- **Where the capability lives.** The app is a shared feature stack over per-head platform seams
  ([ADR-0028](0028-browser-platform-adapters-block.md)); a notification API is inherently per-platform, and
  the browser has no reliable scheduled-while-closed notification at all.
- **Permission & privacy.** OS notifications need a permission (Android 13+ `POST_NOTIFICATIONS`), and a
  notification body is visible on a lock screen — so it must never leak an
  [assignment](../glossary/assignment.md) ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).

**How do we deliver reminders now, keep the account-free/offline promise, respect assignment privacy, and
not paint ourselves into a corner when hosting later enables cross-member push?**

## Decision drivers

- **Ship the value that needs no backend now.** Device-local reminders require neither an account nor an
  origin; gate only what genuinely needs push.
- **Keep [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md).** Reminders must work
  fully offline, account-free, on the device.
- **Respect [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md).** Nothing on a lock
  screen may reveal who drew whom.
- **Follow the platform-seam rule ([ADR-0028](0028-browser-platform-adapters-block.md)).** One abstraction
  in `Ui.Abstractions`, per-head implementations, a deliberate web no-op — exactly like
  [`IShareService`](0028-browser-platform-adapters-block.md) and `IHapticFeedback`.
- **No surprise noise.** Opt-in, quiet by default; ask for permission only when the user first wants a
  reminder.
- **No stale or duplicate reminders.** The schedule must track occasion state (date, draw, wishlist,
  deletion) precisely.
- **Bounded dependency risk.** A local-notification library is acceptable if it is small, MAUI-native, and
  scoped to the device head.

## Considered options

The decision has four coupled axes; the chosen combination is in **Decision outcome**.

**Axis 1 — reach.**
1. **Device-local only** — remind the user about their own occasions, scheduled on their device.
2. **Device-local + cross-member push** — also nudge other members / remind everyone.

**Axis 2 — how the device schedules.**
1. **A local-notification library** (e.g. [Plugin.LocalNotification](https://github.com/thudugala/Plugin.LocalNotification)) wrapped behind a seam.
2. **Shiny** (broader background/jobs framework).
3. **Hand-rolled** per-platform (Android `AlarmManager` + `NotificationManager`).

**Axis 3 — control granularity.**
1. **Global per-kind on/off** in Settings (one toggle per reminder kind, all occasions).
2. **Per-occasion** reminder settings.

**Axis 4 — permission timing.**
1. **On first enable** — ask only when the user turns a reminder on.
2. **On app start** — ask up front.

## Decision outcome

**Chosen: 1 / 1 / 1 / 1** — **device-local only**, behind a **`INotificationScheduler` platform seam**
implemented on the device head with **Plugin.LocalNotification** and a **web no-op**, controlled by a
**global per-kind opt-in** in Settings, with the OS permission requested **on first enable**.

- **`INotificationScheduler` in `Ui.Abstractions`** (references nothing), mirroring `IShareService` /
  `IHapticFeedback`: schedule a notification (stable id, fire time, localized title/body), cancel by id
  (and cancel-all-for-an-occasion), and request/query the OS permission. The **MAUI** head implements it
  over **Plugin.LocalNotification** (added via `Directory.Packages.props`, Central Package Management), in
  the App head's `Platform/` folder beside `MauiShareService`/`MauiHapticFeedback`. The **browser** head is
  a **deliberate no-op** — WASM/PWA cannot reliably fire a scheduled notification while the tab is closed —
  so this is **phone-first**, exactly as haptics are. The Settings toggles still render on the web head but
  explain that reminders need the installed app.
- **A small shared scheduling coordinator** (feature code, head-agnostic) computes the reminder set from
  **local occasion data** and drives the seam. It (re)schedules/cancels on the triggers — occasion date
  set/changed, draw completed, wishlist filled, occasion deleted, and app start (reconcile) — using a
  **stable, deterministic notification id per (occasion, kind)** so a reschedule replaces rather than
  duplicates, and past-due times are skipped.
- **Three reminder kinds, global per-kind toggles, all off by default:** *Draw not done yet* (organizer,
  3 days before), *Event is tomorrow* (anyone holding it, 1 day before), *My wishlist is empty* (self,
  3 days before). Global (not per-occasion) keeps the UI and the stored preference minimal for v1; the
  scheduling model does not preclude per-occasion later.
- **Permission on first enable**, never on app start; a denial degrades to a quiet no-op with an
  informational note.
- **Privacy:** a reminder body carries only the group/occasion name and reason — **never** an assignment
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).

**Cross-member push is explicitly parked**, not rejected: when the hosting/origin decision lands
([ADR-0053](0053-backend-free-first-mobile-release.md)) a future ADR can add a push channel behind the same
or a sibling seam; nothing here forecloses it.

## Consequences

- **Positive:** the highest-value install reason ships now, offline and account-free, with no backend.
- **Positive:** the seam keeps the shared feature code head-agnostic; the web no-op and the eventual iOS
  implementation slot in at one registration line each.
- **Positive:** assignment privacy is preserved by construction — reminders are about dates/status, not
  pairings.
- **Negative / accepted:** a new device dependency (Plugin.LocalNotification), scoped to the App head and
  recorded here; and a permission surface (Android `POST_NOTIFICATIONS`, a manifest entry + a runtime
  request) the app did not have before.
- **Negative / accepted:** the web head genuinely cannot do this; reminders are a phone-only capability in
  v1 (stated in the UI), consistent with [ADR-0054](0054-mobile-native-feel-strategy.md)'s "native feel
  through OS integration".
- **Follow-ups:** implement the MAUI seam + Android permission/manifest; an on-device (Pixel) delivery
  pass per CLAUDE.md; iOS implementation with the iOS head ([ADR-0031](0031-ios-build-host.md)); a future
  ADR for cross-member push once an origin exists; optionally user-adjustable lead times.
- **New risks:** background scheduling reliability varies by Android OEM (battery optimisation may delay a
  notification) — accepted for a reminder (a nudge, not a guarantee); confirmed on the Pixel.

## Pros and cons of the options

### Axis 1 — Device-local only (chosen)
- 👍 No origin/backend needed; ships now; keeps NFR-001 and NFR-002 intact.
- 👎 Cannot nudge other people in v1 (the "remind everyone" wish waits on hosting).

### Axis 1 — Device-local + cross-member push
- 👍 Covers the full brainstorm wish.
- 👎 Needs a production origin + push infra that do not exist; would block the whole feature on the deferred
  hosting decision.

### Axis 2 — Plugin.LocalNotification (chosen)
- 👍 Small, MAUI-native, single-purpose; a thin fit behind the seam.
- 👎 A third-party dependency (scoped to the App head, recorded here).

### Axis 2 — Shiny
- 👍 Powerful background/jobs framework.
- 👎 Far more than reminders need; larger surface and lifecycle to own.

### Axis 2 — Hand-rolled
- 👍 No dependency.
- 👎 Re-implements `AlarmManager`/`NotificationManager` plumbing (and later iOS) for no benefit over a
  focused library.

### Axis 3 — Global per-kind (chosen)
- 👍 Minimal UI and stored preference; matches "a Settings section of switches".
- 👎 No per-occasion nuance yet (deferred; the scheduler allows it later).

### Axis 4 — Permission on first enable (chosen)
- 👍 Asks only when the user has shown intent; no cold up-front prompt.
- 👎 A user who never opens Settings never sees the value — acceptable for an opt-in nicety.

## Links

- Related ADRs: [ADR-0028](0028-browser-platform-adapters-block.md) (platform-adapter seams — this adds a
  sixth-family seam under the same rule), [ADR-0053](0053-backend-free-first-mobile-release.md)
  (backend-free release / the origin gate that keeps cross-member push out of v1),
  [ADR-0054](0054-mobile-native-feel-strategy.md) (native feel via OS integration, not per-platform
  chrome), [ADR-0031](0031-ios-build-host.md) (iOS deferred),
  [ADR-0056](0056-decouple-ui-language-from-formatting-region.md) (date formatting in reminder text)
- arc42 chapters: [05 Building-block view](../arc42/05-building-block-view.md),
  [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md)
- Requirements / use cases:
  [FR-046](../requirements/functional/FR-046-device-local-occasion-reminders.md),
  [UC-SS-023](../use-cases/secret-santa/UC-SS-023-be-reminded-about-my-occasions.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Feature: [FEAT-016](../features/FEAT-016-device-local-occasion-reminders.md) /
  [TP-016](../features/TP-016-device-local-occasion-reminders.md)
