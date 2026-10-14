---
date: 2026-10-14
facilitator: Claude (AI assistant)
participants: [Adrian, Claude]
topic: Secret-Santa Android UX review (on-device test run, Pixel 9 Pro)
status: Concluded
---

# Brainstorming — Secret-Santa Android UX review (2026-10-14)

## Objective / framing question

**Does the shipped Android app (0.1.109) work end to end on a real phone, and where does it make the
organizer or a participant stumble?** A battery of UI tests was driven over `adb` on a Pixel 9 Pro
(Android 17, 960×2142, density 360). Every step was screenshotted and reviewed from a UI/UX point of view
(ease of use, clarity, error-proneness). The findings below are the raw material; the selection section
records what is fixed in the same change.

Flow covered: launch → drawer → create group (storage "On this device") → getting-started checklist →
members (5) → exclusion (mutual) → add event → event details → draw → re-draw → share page → QR → open the
reveal App Link → reveal → My wishlists / My assignments / More / Settings → language EN / FR / DE →
delete group. A second pass (same day) re-checked the re-draw bug and found the exclusions-select defect.

Screenshots were taken into the session scratchpad (`phone/00-launch.png` … `phone/53-excl-guid.png`).
They are **not** committed: several show the tester's real account and other apps' notifications.
Numbers in brackets below refer to them.

## Divergent — raw findings

### Functional bugs

| ID | Finding | Evidence |
|----|---------|----------|
| B1 | Pressing **Draw** a second time showed no re-draw confirmation and did nothing (draw stayed v1). **Not reproducible** in a second pass after an app restart: five variations (linked/unlinked group, after dialogs, exclusions, details edits) all showed the confirmation sheet correctly. Probably a stale dialog/popover state in that long-running session. The web head always confirms. | 21, 23 |
| B2 | **Back with the drawer open leaves the app** instead of closing the drawer. Root cause: `MainActivity`'s back callback only consults the WebView history (never the open drawer/dialog), and once it hits the root it sets `Enabled = false` and is **never re-enabled** — after the first "exit" Back, every later Back leaves the app. | 01 |
| B3 | **A deleted group stays in "My assignments" / "My wishlists".** Signed in, creating *any* group auto-adds and **self-links** the organizer, so `AssignmentPublisher` publishes the group's event registry and the organizer's assignment to the server — even for "On this device" groups, contradicting ADR-0073 ("Privat – wird nirgends geteilt"). `DeleteGroupCommand` only deletes locally; nothing retracts the server projection, so the rows are orphaned forever. ("Testq" and "UI-Test Familie" on the test account are such orphans.) | 41 |
| B4 | **Add-event name is pre-filled but not selected**: typing appends ("Weihnachten 2026Weihnachten 2026"). Same in the carry-forward dialog and the rename sheet; on wide screens the inline field is pre-filled with a real value too. | 14, 15 |
| B5 | **Unsaved edits are silently discarded** on Back (event details; the wishlist editor has the same separate-save design). | 16, 17 |
| B6 | **Opening the soft keyboard pushes the page under the status bar** — the app bar disappears while a dialog field is focused. | 03, 07, 14 |
| B7 | **Exclusions: the recipient select shows a raw member GUID** (`01a13b5d-b1cc-…`) instead of the name after picking (the recipient list is filtered by the giver, so the selected item is re-created and MudSelect falls back to `ToString()` of the value). Screen readers read the GUID too. | 53 |
| B8 | **Create-group dialog: the name field is not focused** and the keyboard does not open (unlike the add-member sheet). | 51 |

Found while testing sign-in, sign-out and storage-mode switching on the web head against a local Aspire
stack. The phone was disconnected by then, and these paths are shared code.

| ID | Finding | Evidence |
|----|---------|----------|
| B9 | **Sharing & sync: "My account → On this device" and "On this device → My account" did nothing.** The radio moved, a reload showed the old mode, and there was no message. `SharingAndSync.razor` had no handler for either transition, although `SyncService.UnclaimAsync` existed. Un-claiming also left `Mode = Account`, so the auto-claim (ADR-0073) would have re-claimed the group. | web 25 |
| B10 | **The sign-in prompt "Import groups created while signed out?" comes back on every sign-in**, even after answering "Cancel" (keep local). It also counted groups deliberately created "On this device" *while signed in*. | — |
| B11 | **Signed in, another account's group cached on the device is listed** as "My account". `VisibleGroupsService` showed *all* groups to any signed-in user. | — |

### Usability — high impact

| ID | Finding | Evidence |
|----|---------|----------|
| U1 | **Exclusions form is cramped**: two selects + button in one row → truncated labels ("Schenke…", "Darf nich…", worse in FR); after choosing both people the button jumps to the next line, so a tap where it was opens a select instead. Help text is abstract. | 10, 12, 13, 37 |
| U2 | **Draw page lacks context**: before the draw only a lone button (no "5 people, 1 exclusion" summary, no note that results stay hidden); after the draw "Draw" stays the filled primary while the next step "Share links" is outlined. | 19, 20 |
| U3 | **Storage "Change" menu**: no checkmark on the current mode, no one-line explanation per mode (Settings explains them well), the open menu covers the Create button. | 04 vs 31 |
| U4 | **Reveal is a flat moment**: no event name/date/budget (ambiguous for people in two exchanges), no next step (add your own wishlist), "No wishlist given" styled as a green success. | 27 |
| U5 | **Members rows**: a big "LINK…" button on every row and "UNLINK" on the organizer's own row — linking is rare and advanced; rows are tall and the name misaligns with the buttons. | 09 |
| U6 | **Add-member sheet closes after each person** — tedious for a 6–12 person family. | 07, 08 |
| U7 | **Group list**: newest group at the bottom (scroll to find what you just made); three control rows per card with a redundant "OPEN" button; unlabelled cloud/phone storage icon. | 00, 33 |
| U8 | **Getting-started checklist items do not read as tappable** (no affordance; the accessibility tree reports them as non-clickable). | 05 |

### Polish

| ID | Finding | Evidence |
|----|---------|----------|
| P1 | Drawer: "Groups" and "All groups" both highlighted (same route); the sync status "Aktuell" reads like an empty section heading. | 01, 30 |
| P2 | Floating action buttons cover content (e.g. "Add event" over the "Sharing & sync" chevron). | 05 |
| P3 | Legal footer links float mid-screen on short pages; wrap awkwardly in FR. | 06, 37 |
| P4 | Event details: "REMOVE BUDGET" shown with no budget set; raw `tt.mm.jjjj` date placeholder; back link reads "OVERVIEW". | 16 |
| P5 | Share page: six filled buttons compete; cards don't show who already got their link. | 24 |
| P6 | Screen-reader labels in English inside the DE/FR UI ("Close", "Toggle <group>"); generic "More actions" on every row. | — |
| P7 | Wording: clunky FR exclusion sentence; long, technical info boxes ("die Betreiberin kann sie technisch lesen" at the top of a main screen). | 37, 29 |
| P8 | Event cards repeat the pattern of U7 (name + "OPEN" button + kebab). | 05 |
| P9 | "More" in the bottom bar opens the same drawer as the hamburger — **kept**: deliberate per the ADR-0070 amendment (two entry points accepted over a hidden menu). | 30 |

### What works well (keep)

- The reveal App Link opens **inside the app**; the draw respected the exclusion (Anna → David).
- Clear confirmations: "Saved.", a delete sheet that names the group, an exclusion summary that reads like a
  sentence.
- Share page with a 0/5 counter and per-person Share / Copy / QR; a large, clear QR dialog.
- The draw result explains why pairings are hidden; the "Drawn" chip on lists.
- Focus + keyboard open automatically in the add-member sheet.
- Instant, complete language switch (incl. the app title); no overflow in DE/EN.

## Clustering / themes

- **Trust & privacy:** B3 (device-only data reaching the server, deleted data lingering), P7.
- **Navigation & system integration:** B2, B6, P1, P9.
- **Forms & input:** B4, B5, B7, B8, U1, U6.
- **Hierarchy & guidance:** U2, U3, U4, U8, P5, P4.
- **Density & clutter:** U5, U7, P2, P3, P8.
- **Accessibility & localization:** B7, P6, P7.

## Convergent — selection

The tester's instruction was "fix everything you found". Everything is selected except where noted.

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| B2 Back closes overlays first; re-enable the callback | 5 | 2 | 5 | Selected |
| B3 Retract the server projection on delete; no automatic self-link for device-only groups | 5 | 3 | 5 | Selected — ADR-0077 |
| B4 Select the suggested name; placeholder instead of value on wide | 4 | 1 | 5 | Selected |
| B5 Warn before leaving with unsaved edits | 4 | 2 | 5 | Selected |
| B6 Keep the app bar visible when the keyboard opens | 4 | 2 | 5 | Selected |
| B7 Exclusion selects show names | 5 | 1 | 5 | Selected |
| B8 Focus the create-group name field | 3 | 1 | 5 | Selected |
| B9–B11 Storage-mode transitions, repeated prompt, cross-account visibility | 5 | 2 | 5 | Selected — ADR-0073 / ADR-0044 amendments |
| B1 Re-draw confirmation | 3 | — | 5 | Parked — not reproducible; re-check on next device run |
| U1–U8, P1–P8 | 2–4 | 1–3 | 5 | Selected |
| P9 Remove "More" duplication | 2 | 1 | 2 | Rejected — ADR-0070 amendment |

## Decisions & selected ideas

- **ADR-0077**: deleting a group retracts its server-side linking projection (queued, offline-safe,
  idempotent), and creating an "On this device" group no longer self-links the signed-in organizer.
- **ADR-0073 amendment**: every storage mode can be reached from every other (B9).
- **ADR-0044 amendment**: visibility is per account (B11), and a "keep local" answer is remembered (B10).
- All other selected items are UI fixes inside the Secret-Santa UI / MAUI head; see the implementation
  status table below.

## Rejected / parked (for the record)

- **P9** — the duplicate drawer entry ("More" + hamburger) is an accepted trade-off recorded in the ADR-0070
  amendment (2026-10-02).
- **B1** — parked as "could not reproduce"; if it recurs, capture `adb logcat` and the WebView console at the
  moment of the tap.

## Implementation status

All fixes landed in the same change as this document. "Verified" means it was driven in a phone-sized
(412×915) browser against the local stack and checked on screen. The Android-only fixes (B2, B6) were
compiled but could not be run on the phone: it was disconnected before the fixes were built.

| ID | Status | Where |
|----|--------|-------|
| B1 | Parked (not reproducible) | — |
| B2 | Fixed — **device check pending** | `Platforms/Android/MainActivity.cs`: Back first closes the top MudBlazor overlay (drawer / dialog / menu) by clicking its scrim. The callback is re-enabled after the exit step. The overlay script was verified in the browser. |
| B3 | Fixed, verified | ADR-0077: `LinkingStore.RetractGroupAsync`, `DELETE /api/v1/groups/{id}/linking`, `SyncState.Retractions`, `SyncingGroupRepository`, `SyncCoordinator`, `Groups.razor`; `RetractGroupLinkingTests`, `SyncingGroupRepositoryTests` |
| B4 | Fixed, verified | `TextInputSheet`, `CarryForwardEventDialog` select the suggestion; the inline field uses it as a placeholder |
| B5 | Fixed, verified | `EventEditor` (NavigationLock + confirm sheet), `MyWishlists` (auto-save + guard) |
| B6 | Fixed — **device check pending** | `App.xaml.cs`: Android soft-input mode `Resize` |
| B7 | Fixed, verified | `Exclusions.razor`: `Guid?` selects with `ToStringFunc` |
| B8 | Fixed, verified | `NewGroupDialog` focuses after the transition |
| B9 | Fixed, verified | `SharingAndSync.razor`, `SyncService` (mode reset on un-claim); `SyncServiceTests` |
| B10 | Fixed, verified | `KeptLocalGroups`, `SignInReconciler`, `Groups.razor` |
| B11 | Fixed, verified | `VisibleGroupsService`, `ClaimableGroup.AccountScope`; `VisibleGroupsServiceTests` |
| U1 | Fixed, verified | Stacked full-width form, static button, sentence-like hint |
| U2 | Fixed, verified | Draw summary (people, exclusions, privacy note); Share links primary, Draw again secondary |
| U3 | Fixed, verified | `StorageModeField`: one-line hint per mode, check mark, opens upward |
| U4 | Fixed, verified | Reveal names the group and event (additive `gr`/`on` payload fields), highlighted result card, neutral wishlist card, next-step line |
| U5 | Fixed, verified | Member actions in the row's overflow menu; one-line rows |
| U6 | Fixed, verified | The add-member sheet loops with "X added — next person?" until Done |
| U7 | Fixed, verified | Pinned first, then newest; whole card opens; storage label next to its icon; "Open" removed |
| U8 | Fixed, verified | Checklist steps are buttons with a chevron (`role=button`) |
| P1 | Fixed, verified | Single "Groups" entry; sync status reads "Synchronisiert – alles auf dem neuesten Stand" |
| P2 | Fixed, verified | The group hub uses an inline "Add event" button instead of a FAB |
| P3 | Fixed, verified | Footer rests at the bottom (`ct-main-fill` / `ct-page`) and wraps |
| P4 | Fixed, verified (date mask: device check pending) | Remove budget only when one is set; back link names the event; empty native date mask hidden |
| P5 | Fixed, verified | Quiet per-person actions; Share/Copy/QR mark the person handed out; QR dialog has one Close |
| P6 | Fixed, verified | `SecretSantaMudLocalizer` translates MudBlazor's built-in labels; rows say "More actions for {name}" |
| P7 | Fixed, verified | Operator-access note is a footnote with calmer wording; wishlist caveat shortened; FR exclusion sentence reworded |
| P8 | Fixed, verified | Event cards follow U7 |
| P9 | Rejected | ADR-0070 amendment |

## Follow-up actions

- [ ] Re-run this battery on the phone after the fixes ship, especially B2 (Back), B6 (keyboard), the
      native date field (P4), and sign-in / sign-out / storage switching on the device. The phone was
      disconnected before the fixes were built; those flows were exercised on the web head. Owner: Adrian.
- [ ] Clean up the orphaned server rows of groups deleted before ADR-0077 ("Testq", "UI-Test Familie" on the
      test account) — they are not in any queue, so they will not be retracted automatically.
- [ ] Add a nightly Secret-Santa E2E run to CI (offered in the same session, not yet decided).
