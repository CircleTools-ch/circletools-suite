---
date: 2026-10-15
facilitator: Claude (AI assistant)
participants: [Adrian, Claude]
topic: Secret-Santa sync & storage-mode test (Pixel 9 Pro + browser, production)
status: Concluded
---

# Brainstorming — Secret-Santa sync & storage-mode test (2026-10-15)

## Objective / framing question

**Do the three storage modes (On this device, Shared by link, My account) and the moves between them
sync correctly between a phone and a browser? And did the fixes from the
[2026-10-14 UX review](2026-10-14-secret-santa-android-ux-review.md) hold up on a real device?**

Setup:

- **Phone:** Pixel 9 Pro (Android 17), Play Store build **0.1.115**, driven over `adb`.
- **Browser:** Playwright at a 412×915 viewport, with a clean profile (storage, service worker and
  caches cleared).
- **Backend:** both against **production** (`secretsanta.circletools.ch`, `api.circletools.ch`).
- **Accounts:** the two test accounts A and B. The phone used A first, then B; the browser used A.
- **Code state:** production runs `main` at `18ddcef`. The uncommitted working-tree changes (ADR-0078
  Amendment 2) were **not** deployed and so were not under test.

Screenshots were kept in the session scratchpad and are **not** committed: they show a real account and
other apps' notifications.

## Divergent — raw findings

### What works (verified on device and in the browser)

| Flow | Result |
|---|---|
| Account sync, browser → phone (W1 imported in the browser) | ✅ after pull-to-refresh |
| Account sync, phone → browser (members added on the phone) | ✅ |
| Account tombstone: delete in the browser, gone on the phone | ✅ |
| Link sync: phone edits reach a join-link holder; a join reaches the phone ("Beigetreten") | ✅ |
| On this device → My account (T1, phone), then visible in the browser | ✅ |
| On this device → Shared by link → On this device; the old join link then shows "Dieser Link funktioniert nicht" | ✅ |
| Link → My account (claim admin), and the account's other device discovers it (`AdminAccount` set) | ✅ |
| Sign out hides account groups; signed in as B, A's groups are not shown (B11, ADR-0078) | ✅ |
| B2 Back closes the drawer and doesn't leave the app | ✅ device |
| B1 Draw again asks for confirmation | ✅ device |
| B4 suggested name is selected · B5 unsaved-changes guard on system Back · B8 name field focused | ✅ device |
| P4 no raw date mask, no "Remove budget" without a budget; native date picker sets the date | ✅ device |
| U2 draw hierarchy · U6 looping add-member sheet · reveal link from the phone opens and reveals in the browser | ✅ |

### Sync bugs

| ID | Finding | Evidence |
|----|---------|----------|
| S1 | **The import prompt never appears on Android.** Sign in on the phone with an account-free group present (twice: account A with T1, account B with W1) and no "Bring your offline data in?" dialog appears. The same flow in the browser shows it. With no answer, `SignInReconciler` takes the Cancel path, so the group is silently added to `KeptLocalGroups` and is never offered again. The reconciler is the only caller of `IDialogService.ShowMessageBoxAsync`. Every other confirmation uses `ConfirmSheet` and displays fine on the phone, and the code comment already names this failure mode ("it never displayed and ShowMessageBoxAsync returned null immediately"). Root cause not verified on device: the Play build isn't debuggable. | phone 05/06, 21/22 |
| S2 | **An account-store group is treated as an admin-claimed shared group right after Import / claim.** `ClaimAsync` sets `Mode = Account`, and both `SharedSyncService.SyncGroupAsync` (`Mode == Account` → `SyncOwnedAsync`) and `RevealSlicePublisher` (`canPublishByOwner = Mode == Account`) read that as "owned shared group". So the client calls `GET /shared/{id}/owned` (404) and `PUT /shared/{id}/owned/reveal-slices` (403) for a group that lives in the account store. ADR-0078 added `AdminAccount` to tell the two kinds apart, but these two call sites still use `Mode`. | browser console after Import of W1 |
| S3 | **A pull resets a record's local sync facts.** `SyncService` (pull) replaces the record with `new SyncedGroupRecord(incoming, version, accountId, Dirty: false)`. That drops `Mode`, `Tokens`, `AdminAccount` and `PendingClaimAccount` to their defaults, so every account-store group ends up `Mode = OnThisDevice` after its first pull. Visible effect: the group hub's **"Teilen & Sync" tile says "Auf diesem Gerät"** for an account group, while the list card and the Sharing & sync page say "Mein Konto". This happens on both phone and web. | phone 08 vs 09; `localStorage` `mode: 0` with `accountScope` set |
| S4 | **A delete doesn't reach the account's other devices for an admin-claimed group.** T3 was created as a link group and claimed into A on the phone, then discovered by the browser and deleted on the phone. It stays in the browser as "Mein Konto" for good, and every cycle logs 404 (`owned`) and 403 (`owned/reveal-slices`). This is exactly the case of **ADR-0078 Amendment 2**, which is still uncommitted. It reproduces on production, so that change is needed. | browser console 14:48–14:58 |
| S5 | **"My account → On this device" promises more than it does.** The prompt says the group "verschwindet von deinen anderen Geräten". But another device follows the un-claim by *keeping* its copy as an "On this device" group (`SyncService` un-claim path: "keeps the local copy … not a delete"). W1 stayed in the browser. Each device now holds a diverging local copy of the same group id. Either the wording or the behaviour must change. | browser after un-claim |
| S6 | **A join-link holder can edit the roster, and the edit is silently lost.** Per ADR-0058 a joined participant edits only their own wishlist / gift note. The browser (join token only) still offered "Mitglied hinzufügen". "Fritz" was saved locally, the record stayed `dirty: true` indefinitely, nothing was pushed, and there was no message. The browser shows a roster nobody else sees. The next newer server version will overwrite it. | browser `localStorage` T2 `dirty: true`, phone never got "Fritz" |
| S7 | **No sync on resume, and no periodic sync.** `AutoSyncService` runs on app start, a local edit, reconnect, and pull-to-refresh only. A phone brought back from the background (or left open) doesn't see changes made on another device until the user pulls down. A browser-side import did not appear on the phone after 25 s plus a background/foreground cycle; pull-to-refresh fetched it at once. | phone 07 |
| S8 | **After a link → My account claim, the join link is no longer shown.** The confirmation says "Der Beitritts-Link bleibt aktiv", but Sharing & sync then only shows "Diese Gruppe gehört dir…". The owner has no way to share the join link with new people. | phone 18 |
| S9 | **Signing out on one device signs out everywhere, with no warning.** `POST /account/logout` calls `RevokeAllAsync` (FR-008 "ends the session everywhere"). Signing out of A on the phone killed the browser's A session (`/account/refresh` 401 from then on). The browser showed no "you were signed out" notice and kept calling authenticated endpoints with the dead token (401s). This is by design per FR-008, but the "Abmelden" button doesn't say so. | browser console 14:52 vs 14:58 |
| S10 | A browser that joined a group by link, signed into the **same** account that later claims admin, keeps the group as "Shared by link" (join holder) instead of following it as "My account". | browser T2 |
| S11 | *(found while fixing S5)* **A stale push re-created a group moved out of the account.** The server refused a push only for a *deleted* group, not for an un-claimed one. A device with an offline edit then put the group back into the account it had just left. | `SyncStore.ApplyChangeAsync` |
| S12 | *(found in the local re-test)* **Edits made during a shared-group reconcile were lost.** `SharedSyncService` (reconcile, owner sync, owned-group discovery, mode transitions) and `RevealSlicePublisher` read the group list, awaited a network call, then saved that stale list. A member added or a draw run during the round trip vanished locally and never reached the server (seen: an admin's "Max" and draw missing online). Every write now re-reads and applies only its own change, and a pull never replaces an unsynced edit at least as new (as in the account sync). Likely related to S6's lost "Fritz" and to edits missing between devices. | local re-test, server body v5 |

### UI / device findings

| ID | Finding | Evidence |
|----|---------|----------|
| D1 | **B6 is not fixed on the device.** With the keyboard open, the create-group and add-event dialogs push the whole page up under the status bar: the app bar disappears and page text overlaps the clock. In the add-member sheet the reverse happens: the app bar stays, but the keyboard **covers the Name field and the Done button**. After the keyboard closes, the page stays scrolled, so the "← back" link hides under the app bar. | phone 02, 10, 11, 23 |
| D2 | The list doesn't refresh after **Import**: W1 still read "Auf diesem Gerät" until a reload. | browser |
| D3 | The import prompt is in **English** while the rest of the screen already switched to the account's German. The UI culture changes on sign-in, but the dialog was built before that. | browser |
| D4 | ~~A reveal link shows the date in the creator's format.~~ **Withdrawn:** the payload carries an ISO date and the reveal page formats it with the *reader's* device region (language and region are decoupled, ADR-0056). The test browser ran with an English region, hence 24/10/2026; a Swiss device shows 24.10.2026. | — |
| D5 | **"Handed out" counts a cancelled share:** opening the share sheet and backing out marked Anna as handed out (1/3). | phone share page |
| D6 | The delete confirmation for an online group (My account / link) doesn't say it also removes the group for everyone and stops the join link (ADR-0078 Amendment 1). The overflow menu stays open behind the sheet. | phone 19 |
| D7 | ~~Accessible names contain a literal `&amp;`.~~ **Withdrawn:** a test-tooling artefact. The uiautomator dump is XML, so it escapes `&` as `&amp;`, and the script that read it didn't unescape. The accessible names are correct. | — |
| D8 | The first tap on "Share links" right after the re-draw sheet closes is swallowed; the second works. | phone 30 |
| D9 | `index.html` has a `<link rel=preload>` with an invalid `href` (console warning on every load). | browser console |
| D10 | **No "change password"** anywhere. Security offers passkeys and email change only, as the tester already noted. | phone Security page |

## Clustering / themes

- **Two "My account" kinds still confused** (S2, S3, S4, S10): code that tells account-store and
  admin-claimed groups apart by `Mode` instead of `AccountScope` / `AdminAccount`, plus a pull that
  forgets the local facts.
- **Promises the UI doesn't keep** (S5, S8, S9, D6): prompts and labels that describe a different
  outcome than the code produces.
- **Silent loss / silent skip** (S1, S6, D2, D5): actions with no feedback, or with a wrong one.
- **Freshness** (S7): changes from other devices arrive only on start or pull-to-refresh.
- **Android keyboard layout** (D1): still open after B6.
- **Localization / a11y** (D3, D4, D7).

## Convergent — selection

Adrian's decision (2026-10-15): **fix everything found**, with S5 and S9 resolved as described below.

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| S4 Ship ADR-0078 Amendment 2 (already written) | 5 | 1 | 5 | Selected |
| S2/S3/S8/S10 Tell the two My account kinds apart by recorded account; keep local facts on pull; owner sees the join link; same-account join holder becomes owner | 5 | 2 | 5 | Selected — ADR-0078 Amendment 3 |
| S1 Import prompt as a sheet that must be answered; no answer ≠ keep local | 5 | 1 | 5 | Selected — ADR-0044 amendment |
| S6 Join-only holders get a read-only group (only the organizer edits) | 4 | 2 | 5 | Selected (conformance to ADR-0058) |
| S5 Un-claim is a move: other devices drop their copy (unsynced edits kept + notice) | 4 | 2 | 5 | Selected — ADR-0073 Amendment 2 |
| S7 Sync on resume + 2-min foreground poll | 4 | 2 | 5 | Selected — ADR-0044 amendment |
| D1 Android keyboard / sheet layout | 4 | 3 | 5 | Selected |
| S9 Sign out = this device; explicit "Sign out on all devices"; revoked session noticed | 4 | 2 | 5 | Selected — ADR-0079 |
| D10 Change password | 4 | 3 | 5 | Selected — FR-049, FEAT-023 |
| S11 Server refuses a plain push for an un-claimed group | 4 | 1 | 5 | Selected (found during S5) |
| S12 Shared-group writes re-read instead of saving a stale copy | 5 | 2 | 5 | Selected (found in the re-test) |
| D2, D3, D5, D6, D8, D9 | 2–3 | 1 | 5 | Selected |
| D4 | — | — | — | Withdrawn (by design, ADR-0056) |
| D7 | — | — | — | Withdrawn (tooling artefact) |

## Decisions & selected ideas

- **S5 — "My account → On this device" is a move.** The device that chooses it keeps the group. Every other
  device of the account removes its copy. A device with unsynced edits keeps them as a separate local copy
  and tells the user once. Reason: users read "move out of my account" like moving a file out of a cloud
  folder. Silent duplicates of a family's group (different draws, different reveal links) are the worse
  surprise. → [ADR-0073](../adr/0073-storage-mode-is-authoritative.md) Amendment 2.
- **S9 — "Sign out" signs out this device only**, as in every mainstream app. "Sign out on all devices" is
  an explicit action on the Security page. Changing the password ends the other sessions. A session ended
  elsewhere is noticed and explained. →
  [ADR-0079](../adr/0079-sign-out-this-device-and-change-password.md),
  [FR-008](../requirements/functional/FR-008-sign-in-with-password.md) (amended),
  [FR-049](../requirements/functional/FR-049-change-password.md),
  [FEAT-023](../features/FEAT-023-session-scope-and-change-password.md).
- **S2/S3/S8/S10** → [ADR-0078](../adr/0078-my-account-groups-record-their-account.md) Amendment 3.
- **S1/S7/D2/D3** → [ADR-0044](../adr/0044-sign-in-reconciliation-and-account-scoped-visibility.md)
  amendment (2026-10-15).
- **S6** needs no new decision: ADR-0058 already says a joined participant edits only their own data. The
  UI is brought in line.

## Rejected / parked (for the record)

- **D4, D7** — withdrawn, not defects (see above).
- **Open question (found while fixing D6):** deleting a **Shared by link** group as its admin-link holder is
  local-only. The online copy stays, and join-link holders and co-admins keep the group until it expires
  (NFR-010). "Make local" does delete it online. Should one admin's delete remove the group for everyone
  (as for an admin-claimed group, ADR-0078 Amendment 1)? Parked for a decision; the delete confirmation for
  such a group keeps its old wording, so it promises nothing it doesn't do.

## Implementation status

"Verified" means driven in a browser (phone and desktop viewports) against the local Aspire stack, with
the screen and console checked. The phone-only fixes compile into the Android head but were not run on
the Pixel yet: installing a debug build means uninstalling the Play Store app (different signature), which
wipes its local data, and that needs Adrian's OK.

| ID | Status | Where |
|----|--------|-------|
| S1 | Fixed, verified (web) | `ReconcileSheet`, `SignInReconciliation` (pending sign-in survives the culture rebuild), `SignInReconciler`; ADR-0044 amendment |
| S2 | Fixed, unit-tested | `SyncedGroupRecordKind.IsAdminClaimedShared`, `SharedSyncService.SyncGroupAsync`, `RevealSlicePublisher`, `AutoSyncService` |
| S3 | Fixed, unit-tested | `SyncService.ApplyPulledChanges` keeps local facts; `EffectiveMode` on the hub and in `ClaimableGroup` |
| S4 | Fixed, unit-tested | ADR-0078 Amendment 2 (`MergeOwnedAsync` 404/403 settle; server `ExistsAsync`) |
| S5 | Fixed, unit + server tests | `SyncService.ApplyUnclaimed`; `KeptAsLocalCopy` notice; `TwoClientSyncTests` |
| S6 | Fixed, verified (web) | `GroupAccess`; hub, event hub, members, exclusions, details, wishlists read-only; Draw/Share redirect; "Your assignment"; repository refuses join-only saves, leaving deletes nothing online |
| S7 | Fixed, compiled | `IAppForeground` (`BrowserForeground`, `MauiForeground`), `ForegroundSyncTrigger` — device check pending |
| S8 | Fixed, verified (web) | `SharedGroupView.JoinToken` (owner read), `SyncOwnedAsync`, join-link card in Sharing & sync |
| S9 | Fixed, verified (web + API) | ADR-0079: `/logout` (this session), `/logout-all`, `SessionNotices` / `SessionNoticeHost` |
| S10 | Fixed, unit-tested | `MergeOwnedAsync` converts a same-account join holder |
| S11 | Fixed, server-tested | `SyncStore.ApplyChangeAsync` refuses a plain push over an un-claim marker |
| S12 | Fixed, unit-tested, verified (web) | `SharedSyncService.UpdateAsync` re-read pattern; `RevealSlicePublisher` |
| D1 | Fixed, compiled | `MainActivity.KeyboardInsetsListener` (edge-to-edge IME inset) — device check pending |
| D2 | Fixed, verified (web) | `Groups.razor` also reloads on `LocalChanged` |
| D3 | Fixed, verified (web) | Prompt re-asked in the account's language after the rebuild; post-login culture re-asserted (`Login.razor`). Known cosmetic: the page *behind* the prompt can keep the old language until the prompt is answered |
| D5 | Fixed (web), compiled (Android) | `ShareOutcome`; `share.js`; Android `ShareChooser` with a target-chosen callback — device check pending |
| D6 | Fixed | Delete wording per kind (`Groups.razor`); menu closes before the sheet |
| D8 | Not reproduced | Re-check on the device |
| D9 | Fixed, verified | Dead `<link rel=preload>` removed |
| D10 | Fixed, verified (web + API) | FR-049: `/change-password`, Security page, password-changed mail (DE/EN/FR) |
| — | Fixed, verified | Found in the re-test: the update prompt registered twice after a language switch (stale .NET reference, duplicate toast) — `js/app.js` in both web heads |

## Follow-up actions

- [x] Triage S1–S10 and D1–D10 — all selected (2026-10-15).
- [ ] Device check on the Pixel (D1, D5, S7, D8; S1 on Android) with a debug build against the local stack
      (`adb reverse tcp:7180 tcp:7180`, `-p:ApiBaseUrl=https://localhost:7180`). Owner: Adrian (OK to replace the
      Play Store install).
- [ ] Decide the open question on deleting a Shared by link group as its admin (see above). Owner: Adrian.
- [ ] Deploy ADR-0078 Amendment 2 (server first, per its deploy order), then re-check S4 on the phone.
- [ ] Clean up the test data: on account A, **T2-Link** (admin-claimed shared group) on the server; the
      browser profile's orphan **T3-Admin** and phantom "Fritz"; the phone's **W1** (local, drawn). The
      phone was left signed in as **account B**.
