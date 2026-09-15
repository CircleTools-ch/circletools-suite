---
id: TP-016
feature: FEAT-016
status: Reviewed
related: [UC-SS-023, FR-046, ADR-0060, NFR-001, NFR-002, NFR-003]
---

> **Executed 2026-09-15 (device delivery pending the Pixel).** Automated: `ReminderPlannerTests` (per-kind
> timing + gates: draw-done skips the draw reminder, filled wishlist skips the wishlist reminder, disabled
> kinds produce nothing, past-due skipped, event-tomorrow fires the day before at 09:00, chronological order)
> and `NotificationPreferencesTests` (all kinds off by default, toggles round-trip, persist across a new
> instance) — part of **Ui 72/72**; Architecture 28/28; LocalizationParity green. Browser-verified the
> web-visible part: Settings **Reminders** section shows the "needs the app" note with **no switches** on web.
> **Pending the Pixel (device-only):** the `POST_NOTIFICATIONS` prompt on first enable, each kind firing at
> its time, and cancel-on-draw / cancel-on-wishlist-filled / cancel-on-delete. No reminder body ever carries
> an assignment (asserted in the planner/scheduler text path).

# TP-016 — Test plan for FEAT-016

## Scope & objectives

Verify [FR-046](../requirements/functional/FR-046-device-local-occasion-reminders.md): opt-in,
**device-local** reminders for a user's own occasions — the three kinds fire at the right time from local
state, the schedule stays correct as occasions change, a reminder **never** carries an assignment, the
control is a global per-kind opt-in (off by default) with the OS permission requested on first enable, and
the **web head is a no-op**. The scheduling/permission/seam strategy under test is
[ADR-0060](../adr/0060-local-notification-scheduling-and-permission.md).

The load-bearing risks (per FEAT-016) are **stale/duplicate reminders** and an **assignment leaking into a
notification body**; the plan weights the scheduler-coordinator unit tests and the no-assignment assertion
accordingly.

## Test levels

- **Unit** — the shared reminder scheduler/coordinator against a **fake `INotificationScheduler`** and a
  controllable `TimeProvider`: no I/O, no device.
- **Component (bUnit)** — the Settings › Notifications section and its permission/web behaviour with a fake
  seam.
- **Architecture** — the seam lives in `Ui.Abstractions`; the MAUI implementation is head-local; the web
  no-op is host-local (ArchUnitNET boundary rules).
- **Platform / E2E (manual + where automatable)** — Android delivery, the `POST_NOTIFICATIONS` prompt,
  reboot persistence — on the Pixel. iOS deferred.
- **Localization** — DE/EN/FR parity for all new strings.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-046 / defaults | Fresh state: all three kinds report **off**; nothing is scheduled | Unit | ✅ |
| FR-046 / off by default | Settings section renders the three toggles, all off | bUnit | ✅ |
| FR-046 / first-enable permission | Enabling the first kind calls the seam's permission request exactly once | bUnit | ✅ |
| FR-046 / draw kind | Organizer, dated occasion, draw not run → a *draw* reminder scheduled at date − 3 days | Unit | ✅ |
| FR-046 / tomorrow kind | Dated occasion → an *event-tomorrow* reminder at date − 1 day | Unit | ✅ |
| FR-046 / empty-wishlist kind | Dated occasion, own wishlist empty → an *empty-wishlist* reminder at date − 3 days | Unit | ✅ |
| FR-046 / past-due skipped | Occasion whose lead-time instant is already past → **not** scheduled | Unit | ✅ |
| FR-046 / cancel on draw | Running the draw cancels that occasion's draw reminder (by its stable id) | Unit | ✅ |
| FR-046 / cancel on wishlist filled | Adding a wishlist item cancels that occasion's empty-wishlist reminder | Unit | ✅ |
| FR-046 / reschedule on date change | Changing the date reschedules to the new date − lead time; the old id is replaced, not duplicated | Unit | ✅ |
| FR-046 / cancel on delete | Deleting the occasion cancels all of its reminders | Unit | ✅ |
| FR-046 / reconcile on start | App-start reconcile recreates what should exist and drops what should not; no duplicates | Unit | ✅ |
| FR-046 / stable id | The same (occasion, kind) always yields the same notification id | Unit | ✅ |
| NFR-002 / no assignment | For every kind, the generated title+body contain the group/occasion name and reason and **no** giver→recipient data | Unit | ✅ |
| FR-046 / kind off = nothing | A disabled kind schedules nothing even when its condition holds | Unit | ✅ |
| FR-046 / permission denied | Denied permission → scheduling is a no-op; the section shows the re-enable note | bUnit | ✅ |
| FR-046 / web no-op | Web head: `NoOpNotificationScheduler` schedules nothing; the section shows the "needs the app" note | bUnit | ✅ |
| ADR-0060 / boundaries | Seam in `Ui.Abstractions`; MAUI impl head-local; web no-op host-local | Architecture | ✅ |
| NFR-003 / parity | Section labels + every reminder title/body present in DE/EN/FR (+ neutral) | Unit (resx parity) | ✅ |
| FR-046 / Android delivery | On the Pixel: enable each kind → the notification fires at the right time; tapping opens the app | Platform/E2E | ⚠️ manual |
| FR-046 / permission prompt | On the Pixel (Android 13+): first enable shows the `POST_NOTIFICATIONS` prompt | Platform/E2E | ⚠️ manual |
| FR-046 / reboot persistence | On the Pixel: a scheduled reminder survives a device restart | Platform/E2E | ⚠️ manual |

## Test data & environments

- **Unit/bUnit:** an in-memory local store seeded with occasions at controlled dates, draw states, and the
  user's own wishlist; a **fake `INotificationScheduler`** recording schedule/cancel calls; a fixed
  `TimeProvider`. No Postgres, no device.
- **Platform:** the Android head deployed to the **Pixel 9 Pro** (USB deploy, per
  [device testing](../deployment/device-testing.md)); Android 13+ for the runtime permission path.
- iOS deferred with the iOS head ([ADR-0031](../adr/0031-ios-build-host.md)).

## Localization & accessibility notes

- DE/EN/FR (+ neutral) parity for the Notifications section labels, the permission-denied and web notes,
  and every reminder title/body; dates in bodies format for the active culture
  ([ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md)).
- Toggles are reachable and labelled for assistive tech; the section is keyboard-operable on the web head
  (where it is informational only).

## Exit criteria

- [ ] All acceptance criteria covered by automated tests (unit + bUnit + architecture + resx parity)
- [ ] Coverage target met (see testing guidelines)
- [ ] The on-device (Pixel) delivery/permission/reboot pass completed and green
- [ ] CI green
