---
date: 2026-10-20
facilitator: Claude (AI assistant)
participants: [Adrian, Claude]
topic: Secret-Santa release check of WP-1…WP-8 (Pixel 9 Pro + browser, production)
status: Draft
---

# Brainstorming — Secret-Santa release check of WP-1…WP-8 (2026-10-20)

## Objective / framing question

**Do the fixes from the [2026-10-16 two-account test](2026-10-16-secret-santa-two-account-device-test.md)
(backlog WP-1…WP-8) hold on the released build against production, and what is still wrong?** This is the
device part of [WP-10](../roadmap/backlog.md#two-account-test-follow-ups--secret-santa-2026-10-16).

Setup:

- **Phone:** Pixel 9 Pro (Android 17, dark mode, German), Play Store build **0.1.120**, account **A**
  (account A), driven over `adb`. Notification permission denied by the user (`USER_FIXED`) and
  left that way.
- **Browser:** Playwright at 412×915, account **B** (account B), English, `localStorage` cleared
  first. A second tab opened a reveal link without signing in as anyone else.
- **Backend:** production (`secretsanta.circletools.ch`, `api.circletools.ch`).
- **Flow:** A signs in → creates *QA-1020* as *Mein Konto · geteilt* → adds Ben, Carla, Dario (and a
  duplicate) → event *Weihnachten 2026* (24.12.2026, max CHF 30, "Bei Oma", reminder override) → shares the
  join link → B joins as Ben, writes his wishlist (with a link) and a gift note → A writes Carla's wishlist,
  adds exclusions, draws → reveal link for Carla opened and edited in the browser → B's and A's assignments →
  drawer, settings, reminders, FR locale, ⋮ menu, signed-in devices → B edits again (sync) → B leaves →
  A signs out and in.

Screenshots stayed in the session scratchpad and are **not** committed (real account, other apps'
notifications).

## Divergent — raw findings

### What works (verified)

| Item | Result |
|---|---|
| Q25 native sign-in has no "stay signed in" box; Q27 web sign-in shows no SSO option | ✅ |
| Q11 keyboard: app bar stays, bottom nav hides, focused field visible (sign-in, create, add member, details) | ✅ |
| T3 sync shown as a cloud-check icon with "Erfolgreich synchronisiert – gerade eben"; Q37 initial avatar | ✅ |
| Q24 create dialog offers Device / Mein Konto / Mein Konto · geteilt with one-line explanations | ✅ |
| Q10 duplicate name: error under the field inside the sheet, text selected, suggestion «Ben B.» | ✅ |
| Q22/I5 add-event date field; event card "24.12.2026 · in 65 Tagen · Wunschlisten 0/4"; *Details* tile summary | ✅ |
| Q28 budget "bis CHF 30" (DE) / "up to CHF 30" (EN) everywhere it showed | ✅ |
| Q18 hub tiles have accessible names with status ("Details, 24.12.2026 · bis CHF 30 · Bei Oma") | ✅ |
| Q6/Q7 join page: organizer disabled ("Organizer"), account option preselected and named | ✅ |
| Q5 participant's *My wishlist* editable, autosaved; organizer sees it read-only ("Ben pflegt das selbst.") | ✅ |
| Q30 "Verbunden" for A and Ben; Ben's badge goes after he leaves | ✅ |
| Q2 B's stored copy of *QA-1020* holds one pairing and only own + recipient wishlists | ✅ |
| Q29 organizer wishlist autosave "Gespeichert ✓" | ✅ |
| Q13a impossible-exclusions warning appears at once and clears when one is removed | ✅ (see U2) |
| T4 after-draw "Soll ich dich vor dem Anlass erinnern?"; per-event override 1/2/3/5/7/14 days / off | ✅ |
| Q8 blocked notifications: dialog and inline note with "Einstellungen öffnen" → Android app settings; switch stays off | ✅ |
| Q20 share counter "Verteilt: 0 / 2"; connected members show "Sieht die Zuteilung in der App" | ✅ |
| D5 cancelled share sheet does not count as handed out | ✅ |
| Q32/I3 share text with group, event, date, place, budget; short link `/r#for=Carla&rt=…` | ✅ |
| FR-050 holder edits own wishlist on the reveal link; the giver (Ben) sees it in the browser | ✅ |
| I8 wishlist URL rendered as "Link öffnen (orellfuessli.ch)" | ✅ |
| T2 drawer: "Alle Gruppen", "Zuletzt geöffnet", initial avatars | ✅ |
| Q34 group ⋮: Rename, Pin, Sharing & sync, Use as template, Delete; Q12 menu closes before the sheet | ✅ |
| Q26/I7 signed-in devices list, "Cet appareil", per-device sign-out; e-mail in the account menu | ✅ |
| WP-10 the app's User-Agent shows as "Secret Santa app · Android" | ✅ |
| Q1 participant *Sharing & sync* shows only "Leave group"; leaving works and is clear | ✅ |
| FR locale on group hub, account menu, security page — no truncation | ✅ |
| Sign out hides account groups, keeps the local one; sign in restores them and syncs | ✅ |
| Browser console: no errors or warnings in the whole run | ✅ |

### Functional bugs

| ID | Finding | Evidence |
|----|---------|----------|
| R1 | **An account-linked giver's *Meine Zuteilungen* freezes at draw time.** Ben added "Socken" after the draw; the organizer's group shows it, but A's *Meine Zuteilungen* still lists only the two earlier items after resume, pull-to-refresh and reopening. Root cause: `LinkingStore.PublishAssignmentsAsync` returns early when `existing >= drawVersion`, so every re-publish of the same draw is dropped — wishlist, gift note, budget, date and place changes after the draw never reach linked givers. The client comment ("a re-publish of unchanged data is a server no-op") assumes only *unchanged* data is skipped. Participants reading through the join link are not affected. | phone; `src/Apps/Main/CircleTools.Main.Service.Repository/Linking/LinkingStore.cs:369` |
| R2 | **The end of a page is hidden behind the bottom nav.** The footer links (Datenschutz, Über & Rechtliches, Ihre Daten löschen) and "v0.1.120" in *Einstellungen* can never be scrolled into view. On event *Details*, after "Gespeichert." appears the page can't scroll far enough to show the reminder dropdown below it. | phone, *Details*, *Einstellungen* |
| R3 | **The wishlist page promises more privacy than the app gives.** B's *My wishlist* (and the reveal-link editor) says "Only the person who gives you a present sees it, together with your note." The organizer sees the wishlist on *Wunschlisten* and the gift note on *Mitglieder*. Either the text or the organizer view is wrong. | browser B; phone |
| R4 | A joined participant's assignment page ends with "You will receive a gift too: tell the organizer what you would like" — Ben maintains his own wishlist; the hint should point to *My wishlist*. | browser B, `/r#for=Ben` |
| R5 | **Q19 still open:** the password is readable in clear text in the Android accessibility tree (`uiautomator dump`) while masked on screen. | phone sign-in |
| R6 | *Withdrawn in part 2:* the field shows "21.10.2026" on screen; "2026-12-24" was only the accessibility value. | phone |
| R7 | *(verify)* The after-draw reminder dialog and the blocked-notifications dialog did not appear in the accessibility tree, although they were on screen. Possibly invisible to TalkBack. | `uiautomator dump` |
| R8 | *My assignments* lists "Testq" and "UI-Test Familie" (Weihnachten 2026), which are not in A's group list. Stale rows of deleted or E2E groups, or groups of another organizer — to verify. | phone |
| R9 | After Ben left, his wishlist and gift note stay in the group and become organizer-editable. TP-024 says leaving "deletes claim and contribution"; the contribution's content stays merged into the group body. The leave prompt doesn't say so. Decide whether that is intended. | phone *Mitglieder*, *Wunschlisten* |

### UI, wording and understandability

| ID | Finding |
|----|---------|
| U1 | The join-link share text is only "Tritt unserer Wichtel-Gruppe bei:" + URL — no group name or organizer, unlike the reveal text. |
| U2 | "Mit diesen Ausschlüssen ist keine Auslosung möglich" for Adri↔Carla and Adri↔Dario in a group of four doesn't say why (Adri↔Ben would be a mutual pair, which the draw forbids). Name the person and the reason. |
| U3 | *Links teilen* rows still differ (Q21): Adri's "Link trotzdem teilen" sits on its own line, Ben's inline next to the hint. |
| U4 | Mixed register: "Ihre Daten löschen" (Sie) next to "du" everywhere else; the French account pages use "vous". |
| U5 | *Sécurité/Sicherheit* has two sections with the same heading "Appareils connectés / Angemeldete Geräte"; the device label "Secret Santa app" is not localized. |
| U6 | A reveal link shared with a German text opens in the browser's language (English here). The link could carry the sender's language as a default. |
| U7 | The group chip says "Konto · geteilt"; the list, create dialog and settings say "Mein Konto · geteilt". |
| U8 | A per-event reminder override ("5 Tage vorher") can be set while reminders are off and notifications blocked, with no hint that nothing will fire. |
| U9 | The leave dialog (web) has an empty heading — an unnamed dialog for screen readers. |

### Part 2 — full run before the release (same day)

Asked for by Adrian after part 1: everything not yet covered, on the same build (0.1.120, R1 not yet
deployed). Adrian allowed granting notifications for the test (revoked again afterwards), a throwaway account
(`+qa1020` alias, deleted again) and confirmed the passkey prompt on the phone.

**Automated suites:** all 12 unit/component/architecture projects pass (1,087 tests); E2E SecretSanta 9/9 and
WebAuthn 2/2 pass.

**Verified on device / in the browser:**

| Item | Result |
|---|---|
| App Link `/r#…` opens in the app (domain verified); a second reveal link replaces the open one; the long `d=` link format still works | ✅ |
| Old data: QA-Familie (drawn with the 10-16 build) opens and reveals for the participant | ✅ |
| Reminders with permission: lead time per kind, time of day, per-event override; alarms registered; DST correct (Q9 — 09:00 CET for a December date planned in CEST) | ✅ |
| A real "before the event" notification arrived at the chosen time ("Erinnerungstest ist morgen") | ✅ (see R12) |
| Offline (Wi-Fi + mobile off): app-bar "Offline" with hint; edits saved locally; synced within 10 s when back online; the other account saw them | ✅ |
| Account-free: on-device group, budget range "CHF 20–40", min > max validation, QR code, guided hand-out ("Mark as handed out" advances) | ✅ |
| Q13a named warnings ("Eva has nobody left to give to"), Q13b "no longer matches the exclusions — draw again?" + "Draw again" chip, Q13c failed re-draw keeps the previous draw with the error | ✅ |
| Signed-out "Shared by link" group with management + join link; second create uses the compact storage line (Q24) | ✅ |
| Q7 signed-out join → "Sign in to keep it on your account" → back on the join page, token not in the URL, account preselected | ✅ (see U14) |
| Q35 import prompt names the groups; an unticked group stays on the device and is not offered again | ✅ (see R11) |
| Storage changes: invite people, hand over without an account, keep in my account, stop sharing — prompts clear, console clean | ✅ |
| Use as template (members + exclusions copied, events/draw fresh); delete group (menu closes first) | ✅ (see U15) |
| Rename on the phone reaches the other account (after the 2-minute poll / reload) | ✅ |
| Light mode on the phone; English on the phone; *My wishlists* add item with autosave | ✅ |
| Sign out one device (stale session) from *Signed-in devices* | ✅ |
| Passkey sign-in on Android (1Password); afterwards the account language applies, as FR-012 specifies | ✅ |
| Throwaway account: register → unverified banner → export JSON → delete → sign-in refused | ✅ |
| Q17 tap fall-through: five fast cancels on a dialog over the bottom nav, nothing behind reacted | ✅ not reproducible |

**New functional bugs:**

| ID | Finding | Evidence |
|----|---------|----------|
| R10 | **Join links don't open the app.** `MainActivity` has App Link filters for `/r`, `/groups` and the account e-mail paths, but not `/join`, so a join link opens in the browser and someone with the app installed joins there, not in the app. | `adb am start …/join#t=…` → Brave; `Platforms/Android/MainActivity.cs` |
| R11 | **Importing a signed-out "Shared by link" group at sign-in drops the sharing.** QA-Link (management + join link) became plain *My account* after "Bring into my account"; *Sharing & sync* shows no join link anymore. ADR-0081 would keep it shared (*My account · shared*). People who joined by that link lose it. | browser, account B |
| R12 | **A reminder disappears from the shade about two minutes after it arrives**, and of two reminders due at the same minute only one was seen. `ReminderScheduler.ReconcileAsync` calls `CancelAllAsync` (→ `LocalNotificationCenter.CancelAll()`) on every reconcile, which runs with each sync, and on Android that also removes notifications already shown. Reproduced: notification posted 11:30:22, gone at 11:31:42 with the next sync. | `ReminderScheduler.cs:97`, `dumpsys notification` |
| R13 | On *My wishlists* the "Change saved · Undo" snackbar stayed for 5+ minutes over the app bar, covering the sync icon and the account button (a tap on the account button hit the snackbar). | phone |
| R14 | Granting the notification permission switched **all three** reminder kinds on, although only "Vor dem Anlass" (and the after-draw offer) had been asked for. | phone *Einstellungen* |

**New UI / wording findings:**

| ID | Finding |
|----|---------|
| U10 | Reminder settings layout: the first two kinds have "Wann" beside the switch, the wishlist kind below it, and the shared "Erinnern um" sits under the wishlist row as if it belonged to it. |
| U11 | Alarms are inexact: planned for 08:59:59.999 with an Android window of up to one hour, so "um 09:00" can arrive close to 10:00. |
| U12 | *My wishlists* edits one item at a time with *Add*, everywhere else a multi-line field; its note "a link already sent keeps showing the old list" is outdated now that online groups use live short links. |
| U13 | "Shared by link — others can open the whole group with a link" / "Anyone with a link below can open this group": since WP-1 a participant sees only their own part. |
| U14 | After the signed-out join → sign-in round trip the member picked before is no longer selected. |
| U15 | "Use as template" adds the signed-in creator as an extra member (5 instead of 4) even when they weren't in the source group. |
| U16 | Offline, *Meine Wunschlisten* and *Meine Zuteilungen* disappear from the bottom nav. |
| U17 | The notification is labelled with the English app name "Secret Santa" next to the German "Wichteln" UI; the channel is the plugin's "General". |

**Not covered:** change password (the step was blocked by the assistant's permission guard — needs a manual
run); iOS; R1 re-check (after the server deploy); joining inside the app (blocked by R10).

### Not covered in part 1

- T4 lead time and time of day in *Einstellungen*, and actual delivery (incl. Q9 DST) — the controls only
  appear with notifications allowed; the phone's permission was not changed. *(Covered in part 2.)*
- Q17 tap fall-through: not seen in this run. A second reveal link arriving while one is open, offline
  behaviour, passkeys, iOS. *(All but iOS covered in part 2.)*

## Clustering / themes

- **Data that stops flowing** (R1, R9): post-draw changes don't reach linked givers; leaving keeps content.
- **Promises the UI doesn't keep** (R3, R4, U8).
- **Layout at the page end** (R2).
- **Accessibility** (R5, R7, U9).
- **Wording and consistency** (R6, U1–U7).

## Convergent — selection

Proposed scores; Adrian decides in triage.

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| R1 Re-publish replaces rows of the same draw when the content changed | 5 | 1 | 5 | Selected — fixed |
| R2 Page bottom padding for the bottom nav; recompute after alerts | 4 | 1 | 5 | Proposed |
| R3 Align privacy text with the organizer's view (or hide it) | 4 | 1 | 5 | Proposed |
| R4, R6, U1–U3, U5, U7, U9 wording/consistency | 2–3 | 1 | 5 | Proposed |
| R5, R7 accessibility checks on the device | 3 | 2 | 5 | Proposed — verify first |
| R8 Find the source of the foreign rows | 3 | 1 | 5 | Proposed — verify first |
| R9 Leave keeps or drops the contribution | 3 | 2 | 4 | Needs a decision |
| U4 One register per locale | 2 | 2 | 4 | Needs a decision (localization guidelines) |
| U6 Reveal page language from the link | 2 | 2 | 4 | Proposed |
| U8 Hint on the per-event override when reminders are off | 2 | 1 | 5 | Proposed |
| R12 Reconcile cancels only pending reminders, never shown ones | 4 | 1 | 5 | Selected — fixed |
| R10 App Link filter for `/join` (and assetlinks covers it) | 4 | 1 | 5 | Selected — fixed |
| R11 Import keeps a shared-by-link group shared (owned, ADR-0081) | 4 | 2 | 5 | Selected — fixed |
| R13 snackbar timeout | 3 | 1 | 5 | Selected — fixed |
| R14 permission grant applies only the wanted switch | 3 | 1 | 5 | Parked — not reproducible |
| R15 after-draw "Erinnere mich" applies once notifications are allowed | 2 | 1 | 5 | Selected — fixed |
| U10–U17 layout and wording | 2 | 1 | 5 | Proposed |

## Decisions & selected ideas

- **R1 — selected and fixed (2026-10-20):** the server still ignores an *older* draw version, but a publish of
  the *current* draw version replaces the event's rows when their content changed (wishlist, gift note,
  budget, event details, names) and stays a no-op when nothing changed. Server-only change; the apps already
  re-publish every sync cycle, so no app release is needed. Takes effect on production after the next server
  deploy; then re-check on the Pixel that a wishlist edited after the draw reaches *Meine Zuteilungen*.
- **R10, R11, R12, R13 — selected and fixed (2026-10-20)** for the same release, on Adrian's go-ahead after
  part 2. ADR amendments: ADR-0053 (R10), ADR-0044 (R11), ADR-0060 (R12).
- **R14 — not reproducible (re-tested 2026-10-20 afternoon on 0.1.120).** (a) All off → blocked → toggle
  "Vor dem Anlass" (refused) → grant → back to the app: only "Vor dem Anlass" comes on. (b) After a draw
  "Erinnere mich" (refused) → "Einstellungen öffnen" → back → grant: nothing comes on. The code only enables
  the kind asked for. Parked; re-open if it shows up again.
- **R15 — new, found while re-testing R14.** Path (b) loses the wish: after "Erinnere mich" with notifications
  blocked, the dialog says reminders work once allowed, but allowing them does not switch "Vor dem Anlass"
  on — the Draw page does not remember the request. **Selected and fixed (2026-10-20)** on Adrian's go-ahead:
  the wish is stored (`NotificationPreferences` wanted kinds) by the Draw page and by *Settings*, and the
  reminder scheduler applies it on start / resume once notifications are allowed (ADR-0060 amendment).
- Everything else: pending Adrian's triage.

## Implementation status

| ID | Status | Where |
|----|--------|-------|
| R10 | Done, Android manifest checked; device check after release | `/join` intent filter in `MainActivity`, `/join*` in the SecretSanta `apple-app-site-association`; routed by the existing `DeepLinkListener`, the join page reads the fragment |
| R11 | Done, unit-tested, verified (web, local stack): a signed-out shared group imported at sign-in shows *My account · shared* with its join link, console clean | `SignInImportPlan` (joined groups not offered; shared-by-link → `ClaimAdminToAccountAsync`), `SignInReconciler`; guard in `SyncService.ClaimAsync`; `SignInImportPlanTests`, `SyncServiceTests.Claim_SkipsAGroupSharedByLink…` |
| R12 | Done, unit-tested, Android build OK; device check after release | `ReminderScheduleDiff` (stable ids, cancel only future changed/removed), `ReminderScheduler.ReconcileAsync`; `INotificationScheduler.GetPendingAsync` / `CancelAsync` replace `CancelAllAsync`; `ReminderScheduleDiffTests` |
| R13 | Done, unit-tested, verified (web, local stack): the undo bar is gone within 9 s | `UndoLastEdit` sets `RequireInteraction = false` (MudBlazor 9 keeps a snackbar with an action open otherwise); `UndoLastEditTests` |
| R15 | Done, unit-tested, Android build OK, web E2E green; device check after release | `NotificationPreferences` wanted kinds (`AddWantedAsync`, `ApplyWantedAsync`; a later switch change drops the wish); `ReminderScheduler.ApplyWantedIfAllowedAsync` on start and resume (`IAppForeground`); Draw page and *Settings* store the wish; `NotificationPreferencesTests` |
| R1 | Done, API-tested; device check after deploy | `LinkingStore.PublishAssignmentsAsync` (`SameRows`); `ILinkingStore` doc; `AssignmentReadTests`: changed re-publish reaches the giver, unchanged re-publish keeps the pairing, an older draw is still ignored after a re-publish |

## Rejected / parked (for the record)

- No import prompt for the empty local group "Test" after sign-in: it is an *On this device* group, which
  ADR-0071 treats as a deliberate choice — not a bug.
- The exclusions warning itself is correct: the draw forbids mutual pairs (`DrawFeasibility`). Only its
  wording is a finding (U2).

## Follow-up actions

- [x] R1 fixed (server); deploy the server and re-check on the Pixel.
- [x] R10, R11, R12, R13 fixed (2026-10-20). After the release, re-check on the Pixel: a join link opens the
      app; a reminder stays in the shade after the next sync; the undo bar hides after a few seconds; a
      signed-out shared group imported at sign-in stays *My account · shared*.
- [x] R14: re-tested on the Pixel, not reproducible (parked). Test data: *Erinnerungstest* in QA-1020 Familie
      is now drawn.
- [x] R15 fixed (2026-10-20). After the release, re-check on the Pixel: draw → "Erinnere mich" with
      notifications blocked → allow them in the system settings → back in the app "Vor dem Anlass" is on.
- [ ] Triage R2–R9 and U1–U17; move the selected items into the backlog.
- [ ] Change password: run manually before the release.
- [ ] Part 2 test data: QA-1020 renamed *QA-1020 Familie* with a second event *Erinnerungstest* (21.10.2026,
      place "Wirklich-Offline"), Ben rejoined; QA-Lokal stays in the Playwright browser (on this device). The
      throwaway account is deleted; the browser is signed out; the phone is signed in as A, German, dark mode,
      notifications denied, reminder switches off.
- [ ] Test data left: *QA-1020* on production (account A, *Mein Konto · geteilt*, 4 members, 1 exclusion,
      drawn, Ben left; Carla's reveal link opened and edited). B's browser profile was cleared at the start
      and holds QA-Familie and SyncTest again after the sync. The phone is signed in as **A**, in German;
      its auto-rotate setting was restored.
