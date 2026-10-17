---
date: 2026-10-16
facilitator: Claude (AI assistant)
participants: [Adrian, Claude]
topic: Secret-Santa full test, two accounts (Pixel 9 Pro + browser, production)
status: Concluded
---

# Brainstorming — Secret-Santa full test, two accounts (2026-10-16)

## Objective / framing question

**Does the released build (with the fixes from the
[2026-10-15 sync test](2026-10-15-secret-santa-sync-device-test.md)) work end to end when two different
accounts share one group? Is the app understandable, complete and well laid out?** Adrian also raised four
topics to look at: the "Create from a circle" button, the drawer's "Groups" entry, the drawer's sync notice
and configurable reminders.

Setup:

- **Phone:** Pixel 9 Pro (Android 17, dark mode), Play Store build **0.1.118**, account **A**, driven over `adb`.
- **Browser:** Playwright at 412×915, clean profile, account **B** (English). A second, isolated browser
  context signed in as **A** was used for the resume-sync check.
- **Backend:** production (`secretsanta.circletools.ch`, `api.circletools.ch`).
- **Flow:** A creates *QA-Familie* (My account) → switches it to Shared by link → B joins as "Ben" with
  "Keep it on my account" → A adds an event (24.12.2026, max CHF 30, "Bei Oma"), a wishlist and runs the
  draw → B opens "Your assignment" → share links, reminders, locales, sign-out/in, import prompt,
  exclusions + impossible re-draw, participant's Sharing & sync.

Screenshots stayed in the session scratchpad and are **not** committed (real account, other apps'
notifications).

## Divergent — raw findings

### What works (verified)

| Flow | Result |
|---|---|
| Join by link as a second account; the join reaches the organizer ("Beigetreten") | ✅ after pull-to-refresh |
| Event, date, budget, location and wishlist reach the participant | ✅ |
| Participant sees "Your assignment" after the draw; reveal shows recipient, budget, date, place | ✅ |
| **S7** foreground poll (web): an edit on the phone showed in B's open page after ~74 s, no reload | ✅ |
| **S7** sync on resume (phone): a member added in the browser appeared right after foregrounding | ✅ |
| **S9** sign-out on the phone leaves the browser session of the same account alive | ✅ |
| **S1** import prompt now appears on Android after sign-in | ✅ |
| **D5** cancelling the share sheet does not count as handed out | ✅ |
| **D1** add-member and create-group dialogs are no longer covered by the keyboard | ✅ (see Q11 for what is left) |
| Add-member loop with Enter; duplicate name is refused | ✅ (see Q10) |
| Impossible draw shows a clear message and keeps the existing draw | ✅ (see Q13) |
| Native date picker; FR locale renders without truncation | ✅ |

### Security & privacy

| ID | Finding | Evidence |
|----|---------|----------|
| Q1 | **A participant can get every person's reveal link through the normal UI.** On *Sharing & sync* a join-only participant has enabled storage radios. "On this device" asks "Make this group on-this-device only? The online copy is removed and shared links stop working." After confirming, nothing is deleted online (no request is sent), but the participant now holds a local, fully editable copy of the group **including the draw**, and *Share links* lists a reveal link for every member. The secret is broken without any tooling. The prompt also promises something that does not happen, and the organizer still shows the participant as "Beigetreten". | browser B, *Share links* after "Make it local" |
| Q2 | **The participant's device stores the complete draw and all wishlists.** B's `localStorage` (`circletools.secretsanta.groups`) holds every `giverMemberId → recipientMemberId` pair. Anyone in the group can read who drew whom with the browser dev tools. The shared-group read for a join token returns the whole document. | `localStorage` dump |
| Q3 | **The linking publish endpoints do not check that the caller organizes the group.** `PUT /groups/{id}/events` and `POST /groups/{id}/assignments/publish` only require a signed-in account. Rows are keyed by `(GroupId, EventId)` and `(GroupId, EventId, GiverAccountId)`, so a second account's publish collides with the organizer's rows → **HTTP 500 on every sync cycle** of the participant. If the participant's device publishes *first*, it owns the rows and the organizer's publish fails; any signed-in account that knows a group id can write a group's event registry and assignment rows. | 500s in B's console from the first sync after joining; `LinkingStore.PublishGroupEventsAsync`, `PublishAssignmentsAsync` |
| Q4 | Root cause on the client side of Q3: `SyncCoordinator.PublishGroupsAsync` publishes the linking projection for **every** local group, including groups this device only joined. | `SyncCoordinator.cs` |
| Q19 | *(verify)* The password field's value appears in clear text in the Android accessibility tree (`uiautomator dump`), although it is masked on screen. Possibly WebView behaviour; worth checking that the input is `type=password` and not exposed to accessibility services. | phone sign-in |

### Functional bugs

| ID | Finding | Evidence |
|----|---------|----------|
| Q5 | **A joined participant cannot enter their own wishlist anywhere.** The event's *Wishlists* page shows all four members read-only, own entry included ("Set each member's wishlist", no save). "Keep it on my account" at join does **not** link the member to the account, so *My wishlists* says "You are not linked in any group yet". Regression of the S6 fix: `GroupAccess` makes the whole group read-only although `ClaimedMemberId` is known. ADR-0058 says a participant edits their own wishlist. | browser B |
| Q6 | The join page offers **"Adri" (already linked to account A, the organizer)** as a pickable identity. A second person can claim to be someone who is already taken. | browser B, join page |
| Q7 | Signed in, the join page still defaults to "Just this device" instead of "Keep it on my account". | browser B |
| Q8 | **Reminder switch lies when notifications are blocked.** Notification permission is denied on the phone (`POST_NOTIFICATIONS granted=false, USER_FIXED`). Turning "Anlass ist morgen" on shows the switch ON, but nothing is stored or scheduled (switch is off again after navigating away, no alarm registered). No message and no way to the system settings. `Settings.ToggleAsync` returns early but the switch keeps its new visual state. | `dumpsys alarm`, phone settings |
| Q9 | **Reminders fire an hour off across a DST change.** `ReminderPlanner.Add` builds the delivery time with *today's* UTC offset (`now.Offset`). Planned in October (CEST, +02:00) for a December event (CET, +01:00), "09:00" becomes **08:00** local; the reverse in spring gives 10:00. | `ReminderPlanner.cs` |
| Q10 | Entering a duplicate member name **closes the add-member sheet**, shows the error on the page behind it, loses the typed text and breaks the "add next" loop. The error belongs inside the sheet. | phone |
| Q11 | **Keyboard and scroll (rest of D1).** (a) With the keyboard open the app bar shifts up under the status bar. (b) On *Sign in* the field being typed in scrolls behind the app bar, so you can't see what you type. (c) The bottom nav rides on top of the keyboard and takes space. (d) After the keyboard closes the page stays scrolled; the breadcrumb sits under the app bar and can't be tapped. (e) A newly opened page inherits the previous page's scroll offset. | phone 07, 12, 27, 29, 50 |
| Q12 | **D6 regression:** the group's overflow menu stays open behind the delete confirmation. | phone 56, 61 |
| Q13 | Exclusions: (a) no warning when they make a draw impossible ("Adri can give to nobody"); (b) after adding exclusions the existing draw stays valid-looking although it breaks them; (c) after a failed re-draw the *Draw* page shows a plain "Draw" button as if nothing was drawn (the hub still says "Ausgelost"). | phone 65–68 |
| Q14 | **Drawer sync line wraps badly.** `MudText Typo.caption` renders an inline `<span>`, so `px-4` only indents the first line; the second line starts at the edge. | phone drawer |
| Q17 | *(seen once, not reproducible)* A tap on the visible centre of "JA, LÖSCHEN" opened the drawer (bottom nav "Mehr") instead. Matches D8 "first tap swallowed". | phone 57 |
| Q18 | Accessibility (web): the group-hub tiles (*Members*, *Exclusions*, *Sharing & sync*) and event-hub tiles (*Details*, *Wishlists*) are buttons **without an accessible name**. | browser snapshot |
| Q20 | "Verteilt 0 / 4" counts the organizer and an already-joined member, who don't need a link. | phone *Share links* |

### UI, wording and understandability

| ID | Finding |
|----|---------|
| Q15 | Switching a My-account group to Shared by link: "Diese Gruppe **wieder** als geteilten Link freigeben?" for a group that was never shared; button "Übergeben" doesn't describe the action; "Admin-Link" is jargon. |
| Q21 | *Share links* rows are ragged: short names stay on one line, others wrap the buttons onto a second line. |
| Q22 | The event card in the group hub shows no date. The event hub's *Details* tile gives no hint when date/budget are missing. |
| Q23 | Signed-in empty state is a bare line ("No groups yet. Add one, or start from one of your circles.") while the signed-out empty state has a proper explanation card. It mentions circles. |
| Q24 | First visit opens a modal storage-mode question before the user has seen anything; it offers "My account" to a signed-out visitor. |
| Q25 | Native app: "Auf diesem Gerät angemeldet bleiben" is unchecked by default. On a personal phone app it should just stay signed in. |
| Q26 | *Security* has a heading "Angemeldete Geräte" but no device list, only a button. *Profile* doesn't show the account's e-mail; the only place is buried in *Security*. |
| Q27 | Web sign-in shows three ways in (password, passkey, "Continue with your CircleTools account"); the third is unclear — it is the same account. |
| Q28 | Max-only budget reads "Budget: 30 CHF". Swiss convention is "CHF 30", and a maximum should read "bis CHF 30". |
| Q29 | Organizer's *Wishlists* page has one Save button per member, while *My wishlists* saves automatically. |
| Q30 | Member badges "verknüpft" (linked to an account) and "Beigetreten" (joined by link) are hard to tell apart and unexplained. |
| Q31 | **A My-account group cannot be shared**, and once it is Shared by link the organizer loses it on their other devices (QA-Familie did not appear in A's second browser). Families want both: the organizer's devices in sync *and* the family invited. |
| Q32 | The share text is a very long raw URL with no context (who, which group, which event, budget). |
| Q33 | Dark mode: white text on the light-blue primary buttons has low contrast. |
| Q34 | The group's overflow menu holds only "Löschen" (no rename, duplicate, storage). |
| Q35 | The import prompt says "2 Gruppen" without naming them, and "Verwerfen" discards all of them irreversibly (one of them held a draw). |
| Q36 | The prominent **"Aus einem Kreis erstellen"** leads to a dead end: "Du hast noch keine Kreise. Erstelle zuerst einen in der Konto-App." — no link, and on the phone there is no "Konto-App". |

### The four topics Adrian raised — proposals

**T1 · "Aus einem Kreis erstellen".** Confirmed a dead end for almost everyone (Q36). Proposal: remove the
button from the list page. Offer circles only *inside* the create-group flow, as a secondary option
("Mitglieder aus einem Kreis übernehmen"), and only when the signed-in account has at least one circle with
two or more people. Change the empty-state text so it doesn't mention circles (Q23).

**T2 · Drawer "Gruppen" looks like a group.** It uses the same icon, weight and indentation as the
quick-access groups below it. Proposal:

- rename it **"Alle Gruppen"** and give it a distinct icon (`ViewList` or `Home`);
- put a small caption **"Zuletzt geöffnet"** above the quick-access groups (as "Pinned" already has);
- show the quick-access groups with a lighter style: a round initial avatar instead of the groups icon,
  regular weight, slight indent.

**T3 · "Synchronisiert – alles auf dem neuesten Stand".** It wraps badly (Q14) and the wording is vague.
Proposal: make the status quiet when all is well and loud only when action is needed.

- All fine: one short line with a cloud-check icon, "Synchronisiert · vor 2 Min.", or no line at all.
- Offline / failed / storage full: a coloured row with a clear next step ("Offline – Änderungen werden
  gesendet, sobald du online bist", "Synchronisierung fehlgeschlagen · Erneut versuchen").
- Render it as a block element (fixes the wrap).
- Alternative: move it out of the drawer into the account menu next to "Angemeldet als …".

**T4 · Reminders with lead time and time of day.** Today: three switches, fixed leads (draw 3 days,
event 1 day, wishlist 3 days), fixed 09:00, off by default, per device. Proposal:

- per kind a lead time ("5 Tage vorher", choices 1/2/3/5/7/14) and one shared time of day (default 09:00);
- rename "Anlass ist morgen" to "Vor dem Anlass" now that the lead time is configurable;
- optionally a per-event override on the event's *Details* ("Erinnerung: 5 Tage vorher um 09:00");
- show the blocked-permission state with a link to the system settings (Q8);
- fix the DST offset (Q9);
- offer to switch reminders on in context, e.g. after the draw ("Soll ich dich vor dem Anlass erinnern?").

This changes FR-046 / FEAT-016 and needs a requirement update before the code.

### Other ideas

- **I1** Participants as first-class members: a joined person (with or without account) edits their own
  wishlist and gift note, sees "Your assignment", and can **leave** the group (Q1, Q5).
- **I2** Strip the draw from what a join token can read; serve a participant only their own pairing (Q2).
- **I3** Short reveal/join links (server token) and a share text with context: "🎁 Wichteln QA-Familie –
  Weihnachten 24.12.2026, bis CHF 30. Dein Link: …" (Q32).
- **I4** "Shared by link" + "My account" together: the organizer's devices stay in sync *and* the family is
  invited (Q31).
- **I5** Event card shows date and countdown ("in 69 Tagen") and a short status (draw, wishlists 2/4).
- **I6** Ask for the storage mode at the first group creation instead of in a modal on first visit (Q24).
- **I7** Device list with "sign out this device" under *Angemeldete Geräte* (Q26).
- **I8** Wishlist items with optional link and price; autosave everywhere (Q29).

## Clustering / themes

- **The secret is not safe from participants** (Q1, Q2, Q3, Q4): participant devices hold the draw, can
  turn it into a local organizer copy, and can write server rows that belong to the organizer.
- **Participants can't participate** (Q5, Q6, Q7, Q20, Q30): joining works, but the joined person has no
  wishlist, can pick a taken identity and isn't counted correctly.
- **Promises the UI doesn't keep** (Q1 prompt, Q8 switch, Q13c, Q15, Q36): wording or state that says
  something else happened.
- **Android keyboard & scroll** (Q11, Q12, Q17): still the main source of on-device roughness.
- **Reminders** (T4, Q8, Q9).
- **Navigation clarity** (T1, T2, T3, Q22, Q23, Q34).

## Convergent — selection

Scores as proposed; Adrian selected every item in a one-by-one triage (2026-10-16).

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Q1 + Q2 + I2 Participant can't become organizer; join token gets only its own pairing | 5 | 3 | 5 | Selected |
| Q3 + Q4 Organizer check on publish endpoints; client publishes only own groups | 5 | 2 | 5 | Selected |
| Q5 + I1 Participant edits own wishlist; account join links the member | 5 | 2 | 5 | Selected |
| Q6/Q7 Hide taken members on join; default to account when signed in | 4 | 1 | 5 | Selected |
| Q8 + Q9 + T4 Reminders: permission state, DST fix, lead time + time of day | 4 | 2 | 5 | Selected (needs FR-046 update) |
| Q11 Keyboard/scroll rest | 4 | 3 | 5 | Selected |
| T1 Hide "Aus einem Kreis erstellen" | 4 | 1 | 5 | Selected |
| T2 Drawer "Alle Gruppen" + "Zuletzt geöffnet" | 3 | 1 | 5 | Selected |
| T3 Quiet sync status, fix wrap | 3 | 1 | 5 | Selected |
| Q10, Q12, Q13, Q14, Q15, Q18, Q20–Q30, Q32–Q35 | 2–3 | 1 | 5 | Selected |
| Q31 + I4 Shared and account-synced at once | 4 | 4 | 4 | Selected — ADR |
| Q17, Q19 | — | — | — | Selected — verify first |

## Decisions & selected ideas

Adrian triages the findings one by one (2026-10-16). Decided so far:

- **Q31 / I4 — selected:** a Shared-by-link group created or shared by a signed-in organizer is owned by
  their account (the existing admin-claim path), so it syncs to all their devices and keeps its join link.
  A *My account* group offers "invite people", which turns it into such an owned shared group. Needs an ADR.
- **Q1 — selected:** a participant's *Sharing & sync* shows no storage options, only who they joined as and
  **"Leave group"** (removes the local copy and releases their claim on the server, so the organizer sees
  the person as not joined). A joined group can never become an editable local copy.
- **Q2 / I2 — selected:** a read with a join token returns a redacted document: no draw pairings except
  the one of the member this device claimed. Admin-link and owner reads stay full. Participant devices
  drop the stored full draw on their next sync. Needs an ADR.
- **Q3 — selected:** `PUT /groups/{id}/events` and `POST /groups/{id}/assignments/publish` accept only the
  group's organizer (account owner, or admin-link holder for a shared group); anyone else gets 403, never
  500. The first organizer to publish is recorded and only they replace the rows. Server tests for
  organizer / participant / stranger.
- **Q4 — selected:** the client publishes the linking projection only for groups this device organizes
  (local, account-store, admin-link or owner); join-only groups are skipped. Unit-tested.
- **Q19 — selected (verify first):** check every password field is a real password input with the right
  `autocomplete`; compare on the phone against a plain Chrome password field. If ours, fix so accessibility
  services get only the masked value; if Android behaviour, record as not ours.
- **Q5 / I1 — selected:** a participant's *Wishlists* page shows only their own entry, editable and saved
  automatically (others hidden; the recipient's list is on *Your assignment*). It reaches the organizer via
  the join link. "Keep it on my account" really links the member to the account (*My wishlists*, *My
  assignments* on all devices). Same for the participant's own gift note.
- **Q6 — selected:** on the join page, taken members are shown disabled with a note ("Organizer", "already
  joined"); the organizer's own member is never claimable; "release" stays easy to find in the member's
  menu; an all-taken group says so instead of showing an empty list.
- **Q7 — selected:** signed in, the join page preselects "Keep it on my account" and names the account;
  signed out, "Just this device" stays default and the account option links to sign-in, which returns to
  the join page with the invitation intact.
- **Q8 — selected:** a refused permission leaves the switch off and shows a note with "Open settings"
  (deep link to the app's notification settings); on return the app re-checks and applies the wanted
  switch. The note also shows when notifications were blocked later. The web head says reminders live in
  the app instead of offering inert switches.
- **Q9 — selected:** the delivery time is the wanted local time on the target date, resolved with the
  device's time-zone rules for that date (not today's offset). Unit tests for both DST directions and the
  switch night.
- **Q10 — selected:** a duplicate name keeps the add-member sheet open, shows the error under the field,
  keeps the text selected and the keyboard up, and suggests a distinguishing letter. Same for renames.
- **Q11 — selected:** with the keyboard open the content area shrinks (the app bar never moves), the
  focused field scrolls into view below the app bar, the bottom nav hides; closing the keyboard undoes
  the leftover scroll; a new page starts at the top, Back restores the position. Verified on the Pixel on
  sign-in, create group, add member, event details, wishlists, exclusions.
- **Q12 — selected:** every ⋮ menu that opens a sheet (group list, event card, member list) closes before
  the sheet opens. Checked on the Pixel.
- **Q13 — selected:** (a) the exclusions page warns at once when a person is left with nobody to give to /
  receive from, or the set makes a draw impossible (still allowed); (b) a drawn event whose draw breaks the
  current exclusions shows "no longer matches the exclusions — draw again?"; (c) after a failed re-draw the
  Draw page keeps showing the current draw state with the error above it.
- **Q14 — selected:** the sync status renders as a block element (both lines align).
- **T3 — selected, Adrian's variant:** synced = a cloud-check **icon only**; hover/tap shows "Erfolgreich
  synchronisiert · vor 2 Min.". Syncing = a spinner only. Offline / failed / storage full = an icon plus a
  short hint text; tapping it shows details where they help (e.g. failed → reason + "Try again"; storage
  full → what to free up; offline → that changes are sent later).
- **Q17 — selected (investigate first):** check the sheet's stacking against the bottom nav and tap
  repeatedly on the Pixel during/after the animation. If confirmed, the sheet and its backdrop always take
  the touch and/or the bottom nav hides while a sheet is open; otherwise record as not reproducible.
- **Q18 — selected:** every hub tile gets an accessible name (title + status, e.g. "Members, 4"); other
  tile-style buttons checked too; a component/E2E check fails on unnamed tiles.
- **Q20 — selected:** members who see their assignment in the app (account-linked or joined) count as
  covered ("Sees the assignment in the app", share actions behind "Share link anyway"); the counter counts
  only those who need a link ("0 / 2", "Everyone has their assignment"); the organizer gets a hint to
  *My assignments*.
- **Q15 — selected:** separate prompts for first-time sharing ("Invite people? … the group stays in your
  account", button "Invite") and releasing an owned group to link-only (button "Release"); "admin link"
  replaced in visible text by a plain term ("management link — to co-organize") explained where shown.
  DE/EN/FR. Fits the Q31 "invite people" action.
- **Q21 — selected:** every *Share links* row has the same layout — name + status on the first line,
  actions on the second; on wide screens one line, but consistently; long names wrap.
- **Q22 / I5 — selected:** the event card shows date + countdown ("24.12.2026 · in 69 days"), draw status
  and wishlist progress ("Wishlists 2/4"); events ordered upcoming first, past ones last and greyed; the
  *Details* tile says what is missing ("Date and budget missing") or summarises ("24.12.2026 · up to CHF
  30 · Bei Oma"); the add-event dialog offers a date field.
- **Q23 — selected:** signed in, the empty list shows the same welcome card as signed out (without the
  sign-in hint), plus a pointer to *My assignments* when the account has memberships; no circles wording.
- **T1 + Q36 — selected:** the "Create from a circle" button leaves the group list. *Create group* shows a
  secondary "Take members from a circle" option only when the account has a circle with 2+ people. The
  circle page, if opened without circles, links to the account app on the web instead of naming an app
  that doesn't exist on the phone.
- **T2 — selected:** the drawer entry becomes **"All groups"** (distinct list/home icon, bold); a small
  "Recently opened" caption above the quick-access groups; those show a round first-letter avatar, regular
  weight, slight indent.
- **T4 — selected (all points):** per-kind lead time (1/2/3/5/7/14 days; defaults 3/1/3); one time of day
  for all reminders (default 09:00); "Event is tomorrow" becomes "Before the event" with adaptive text
  ("in 5 days"); per-event override on *Details* (as in settings / custom / off); after a successful draw,
  offer "Remind me before the event?" (asks for permission in context). Settings stay per device.
  FR-046 and FEAT-016 are updated first.
- **Q24 / I6 — selected:** no storage modal on first visit; the choice lives in *Create group* (expanded with
  a one-line explanation per option the first time, compact afterwards); signed out, "My account (sign-in
  needed)" leads to sign-in and back. Default stays in Settings. ADR-0071 amendment.
- **Q25 — selected:** the native heads have no "stay signed in" checkbox and always stay signed in (device
  lock protects; sign out / sign out everywhere remain). Web unchanged. FR-008 documents the app behaviour.
- **Q26 / I7 — selected (all):** *Signed-in devices* lists each active session (device, app/browser, last
  active), marks this device, and signs out one device; "sign out everywhere" stays. Needs a server
  session list + single revoke and an ADR-0079 amendment. The e-mail shows in the account menu and at the
  top of *Profile*.
- **Q27 — selected:** the SSO option ("Continue with your CircleTools account", FR-031/ADR-0039) shows only
  when the browser is known to have a session on the authority; if that can't be detected cheaply it is
  hidden for now. When shown it reads as a one-click continuation ("You're signed in on circletools.ch —
  continue with one click"), not a third sign-in method.
- **Q28 — selected:** one budget format everywhere (reveal, my assignments, details, calendar, share
  text): range "CHF 20–30", max only "up to CHF 30", min only "from CHF 20", equal "CHF 30"; currency
  position follows the user's region (de-CH "CHF 30", fr-CH "30 CHF"). Unit tests per combination/locale.
- **Q29 — selected:** wishlists autosave everywhere (debounced while typing and on blur), with a small
  "Saved ✓" per box; no Save buttons; a failed save shows the error and keeps the text.
- **Q30 — selected:** one badge **"Verbunden"** (EN "Connected", FR "Connecté·e") for a member who gets the
  result in the app (account-linked or joined); tapping it explains how (joined by link, linked to account
  …) and offers "Release" (Q6). Members without the badge need a reveal link (matches Q20).
- **Q32 / I3 — selected (a + b):** (a) the share message carries context — group, event, date, place,
  budget (Q28 format), the person's name and a one-line "only you see who you got" — DE/EN/FR. (b) Short
  reveal links (`/r/<code>`) for online groups (Shared by link, My account), content fetched from the
  server, with a non-revealing link preview; local-only groups keep the self-contained long link. Needs
  an ADR.
- **Q33 — selected:** every theme colour pair passes WCAG 4.5:1 in light and dark (dark-mode primary
  buttons get dark text or a deeper blue; "Drawn" chip, orange warning text, red delete, grey hints
  checked). Fixed centrally in `CircleToolsTheme`; verified on the Pixel in both modes.
- **Q34 — selected (incl. duplicate):** the group ⋮ menu (list and group page) offers Rename, Pin/Unpin,
  Sharing & sync, "Use as template for a new group" (copies members + exclusions, no events/draw), then
  Delete last and set apart. A joined group shows "Leave group" instead of Delete.
- **Q35 — selected:** the import prompt lists the groups by name with a summary ("4 members · drawn"), all
  ticked by default; "Import" moves the ticked ones into the account, unticked ones stay on this device
  (not asked again); "Later" asks again at the next sign-in. No discard in the prompt (deleting stays in
  the group menu). ADR-0044 amendment.
- **I8 — selected:** wishlist items get an optional link and price; pasted URLs become links; the giver
  sees clickable links, prices and an "over budget" hint. The wishlist format changes (sync, reveal links,
  server copies); old lists stay valid as text-only items and older clients still show the text. Needs an
  ADR.
  *Revised the same day after trying it:* the typed "·" convention for price and link was confusing, and
  prices are dropped. Links are now simply recognised wherever they are pasted ([ADR-0083](../adr/0083-links-in-wishlist-items.md)).

- **Q37 — selected (raised by Adrian after the triage):** the app-bar account button shows the state
  without opening it. Signed in: a round avatar with the display name's initial, tooltip / long-press
  "Signed in as Adri (e-mail)"; a small orange dot while the e-mail is unverified. Signed out: an outlined
  person icon on phones, a text button "Sign in" on wide screens. The accessible name carries the state.
  Shared `AccountMenu`, so the Main app gets it too.

Every finding (Q1–Q37), topic (T1–T4) and idea (I1–I8) is selected. The work is sliced into packages
WP-1 … WP-8 in the [backlog](../roadmap/backlog.md#two-account-test-follow-ups--secret-santa-2026-10-16).

## Rejected / parked (for the record)

- The reveal-link payload is compressed, not encrypted (anyone with the link can decode it without pressing
  "Reveal"). Accepted by design for offline links; listed so it isn't rediscovered as a bug.
- The "100 %" loading screen in some automated snapshots was screenshot timing, not a defect.

## Implementation status

"Verified" means driven in a browser against a local stack (the new API on `localhost:7180`, the web app on
`localhost:5081`) with two accounts in separate browser contexts, screens and console checked. Device checks
on the Pixel follow once a build is installed.

| ID | Status | Where |
|----|--------|-------|
| Q1 | Done, verified (web) | `SharedSyncService.MakeLocalAsync` refuses join-only, `LeaveAsync`; *Sharing & sync* participant view; list menu "Leave group" |
| Q2 | Done, verified (web) | `SharedGroupBody.Redact`, `SharedGroupStore.GetAsync` (participant key / account proof); B's `localStorage` held one pairing, no foreign wishlists, gift notes or account ids |
| Q3 | Done, API-tested | `GroupOrganizerCheck` on both linking publish endpoints; collision rows of another organizer replaced; 403 not 500 |
| Q4 | Done, unit-tested | `AssignmentPublisher.PublishOrganizedAsync` (skips join-only, sends `X-Admin-Token`) |
| Q5 | Done, verified (web) | `PUT /shared/{token}/me`, entries re-applied on organizer push; participant *Wishlists* page (own entry, autosave, gift note); organizer sees it read-only; *My wishlists / My assignments* list joined groups; `GET /shared/joined` + `DiscoverJoinedAsync` |
| Q6 | Done, verified (web) | `takenMembers` in the view; join page disables taken members with the reason; all-taken message |
| Q7 | Done, verified (web) | Account preselected and named when signed in; sign-in returns to the join page (`PendingJoinLink`, token not in the URL) |
| Q19 | Checked in code | All password inputs are `type=password` with `autocomplete`; the Pixel comparison is part of the device check |
| Q20 | Done, verified (web) | Counter counts only members who need a link ("0 / 2"); "That's you …" / "Sees the assignment in the app" + "Share link anyway" |
| Q30 | Done, verified (web) | One "Verbunden" badge with a tooltip saying how |
| — | Fixed, found while verifying | The client ignored `rewritten` after a push, so the organizer never saw a participant's entry written in between (same version, different body) — now adopted at once (`AdoptRewrittenAsync`, unit-tested) |
| — | Fixed, found while testing | A pending own entry was never cleared after it was sent (record equality over lists) — compared by content now |
| — | Fixed, found while reading | An account person-claim trusted the account id in the request body — now bound to the caller's bearer only |
| — | Fixed, found while reading | Account deletion left the account's person claims (and now its participant entries) behind — released with the other cleanups |
| Q31 | Done, verified (web, two devices) | ADR-0081; `ShareOwnedAsync`, `StopSharingAsync`, owner release endpoint; signed-in choices; `StorageKind` chips; same-cycle rediscovery after "Invite people" |
| Q15 | Done | Invite / stop-sharing / hand-over prompts; "management link" replaces "admin link" in DE/EN/FR |
| Q24 | Done, verified (web) | No onboarding dialog; first create explains the options; signed-out account choice routes through sign-in (`?create=account`) |
| Q35 | Done, verified (web) | `ReconcileSheet` names the groups, all ticked, Import / Later, no discard |
| — | Fixed, found while reading | `ClaimableGroup.HasCurrentDraw` read the legacy group-level draws only, so it was always false |
| T4 | Done, unit-tested, Android build OK | Lead per kind (1/2/3/5/7/14), time of day, "Before the event" with "in N days" text, per-occasion override on *Details*, after-draw offer; FR-046 / FEAT-016 amended — device check pending |
| Q8 | Done, unit-tested, Android build OK | Refused permission keeps the switch off (`@key` re-create), note + "Open settings" (`AppInfo.ShowSettingsUI`), re-check on resume applies the wanted switch; web says "in the app" — device check pending |
| Q9 | Done, unit-tested | `ReminderPlanner` resolves each delivery time with the target date's own offset (`TimeProvider.LocalTimeZone`); tests for both DST directions and the spring-forward gap |

The owner-release gap below is closed by WP-2 (`DELETE /shared/{id}/owned/claims/{memberId}`).

Open, found along the way: the owner of an account-owned shared group cannot *release* a joined person (the
server takes only an admin token) — needed by WP-2, where every signed-in organizer's shared group becomes
owned. The first-run storage question's choice was not applied to the create dialog (Q24 removes that dialog).

## Follow-up actions

- [x] Triage Q1–Q36, T1–T4 and I1–I8 — all selected (2026-10-16).
- [ ] Implement in the order of backlog work packages WP-1 … WP-8; WP-1 (secrecy, server authorization,
      participants) first and before the next release.
- [ ] Test data left on production / devices: *QA-Familie* (Shared by link, organizer A on the phone, with
      3 exclusions); B's browser profile holds a local copy of it after the "Make it local" test; *T2-Link*
      (account A) gained a member "ResumeTest". The phone was left signed in as **A**, in German.
