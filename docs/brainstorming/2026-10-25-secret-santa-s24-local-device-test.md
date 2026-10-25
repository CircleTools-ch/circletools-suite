---
date: 2026-10-25
facilitator: Claude (AI assistant)
participants: [Adrian, Claude]
topic: Secret-Santa first-install test on a Galaxy S24 against the local stack (after ADR-0086)
status: Draft
---

# Brainstorming — Secret-Santa first-install test on a Galaxy S24, local stack (2026-10-25)

## Objective / framing question

**Does the ADR-0086 build ("one way to be in a group", [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md))
work on a phone that never had the app, and what breaks?** Run unattended while the owner was away: find issues,
fix them, redeploy to the phone, repeat. Nothing was committed.

Setup:

- **Phone:** Samsung Galaxy S24 (SM-S926B, Android 14, German), app never installed before, built from the working
  tree and deployed over USB (`dotnet build … -t:Run -p:ApiBaseUrl=https://localhost:7180
  -p:RevealBaseUrl=https://localhost:7081`), the local Aspire stack reached through `adb reverse`. Driven over `adb`
  and over the WebView's DevTools protocol (Playwright `connectOverCDP`).
- **Second person:** Playwright on the PC against the same local stack (organizer "Olga", plus fresh browsers that
  join by link).
- **Accounts:** throw-away local accounts (`…@example.test`, Mailpit); nothing from production.
- **Offline:** simulated by removing the API tunnel (`adb reverse --remove tcp:7180`).

Screenshots stayed in the session scratchpad and are not committed.

## Divergent — raw findings

### What works (verified on the phone)

| Item | Result |
|---|---|
| First run, device group, members, event, draw, reminder offer + notification permission | ✅ |
| Share page, reveal (long link) in Chrome | ✅ |
| Put online without an account (organizer link), join link, Chrome joins as Ben (device key) | ✅ Ben's wishlist reaches the app, read-only for the organizer |
| Register, verify, sign in in the app; take over into the account; "Das bin ich" + *Meine Zuteilung* | ✅ |
| PC organizer invites the phone's account; invitation after the periodic sync; accept → participant page; reveal | ✅ |
| Wishlist saved offline as a participant → reaches the organizer after reconnect | ✅ |
| App Link `https://secretsanta.circletools.ch/join#t=…` opens the join page in the app | ✅ |
| Leave a group taken part in (both sides) | ✅ |
| Free a spot (server side) | ✅ (screen refresh: F10) |
| FR and EN walk of the main screens | ✅ (one term, F9) |
| Account deletion with "keep online" — participant keeps access; the group lives on under an organizer link | ✅ web and phone (device side: F12) |
| Data export (*Daten & Konto → Herunterladen*) | ✅ Android share sheet with `circletools-export-2026-10-25.json` |
| Rejoin a group left earlier, through the account | ✅ |
| Dark mode (system setting) | ✅ app follows it after a restart; home, group, settings read well |

### Findings and what was done

| # | Finding | Fix (working tree, uncommitted) |
|---|---|---|
| F1 | Start page: the *Gruppe erstellen* FAB covered the first-run card's sign-in link; the card has its own create button. | FAB hidden while the first-run card shows. |
| F2 | Create dialog with the keyboard open: title cut off under the status bar, app bar pushed up. Root cause: the keyboard listener padded the content by *keyboard − navigation bar*, but on Android 14 the content runs behind the navigation bar, so the WebView stayed a navigation bar too tall and Android panned it. | Pad by the content's real overlap with the keyboard (`MainActivity.KeyboardInsetsListener`); centred dialogs capped by the safe-area insets (`app.css`). Verified: layout height = visible height, no pan. **Re-check on the Pixel (Android 15+).** |
| F3 | A member-kept wishlist / gift note is read-only but looked editable. | Filled field + lock icon. |
| F4 | Bottom nav covered the page's last content (MudBlazor's `py-4 !important` beat `.ct-has-bottomnav`). | `!important` on the bottom-nav padding. |
| F5 | **Serious:** after a cold start without a reachable server the app looked signed out and hid every group owned or taken part in through the account; nothing renewed the session when the network came back. | Remember the session's account next to the refresh token (display state only); `SessionAccount` shows its groups while the session waits; `AccountSession.ResumeAsync` on every sync trigger. [ADR-0021 amendment (Proposed)](../adr/0021-web-auth-token-model.md#amendment-2026-10-25-proposed--offline-is-not-signed-out). Verified on the phone: groups stay visible offline; resume / pull-to-refresh renews the session. |
| F6 | A join link opened on a device already in the group (as Kim, through the account) asked "who are you?" and offered a second spot; an account join was refused with a misleading "taken meanwhile". | Join page shows *Du bist schon dabei — Gruppe öffnen* (organizer: *Du organisierst diese Gruppe*). |
| F7 | *Ihre Daten löschen* and the privacy page still described the pre-ADR-0086 model ("without an account nothing leaves your device", *My wishlists*, shared-group hand-over at deletion). | Rewritten in EN/DE/FR to match the server (owned-group choice, leave, member-kept entries, groups without an account on the server, one-year retention of owner-less groups); dates 2026-10-25. **Legal text — owner to review.** |
| F8 | The app's *Not found* page was hard-coded English. | Uses the existing localized strings. |
| F9 | FR used *tirage* and *attribution* for the same thing. | *attribution* throughout. |
| F10 | **Bug (web + app):** *Platz freigeben* released the member on the server, but the roster kept showing *Verbunden* until the next sync. Menu actions run after the menu closes, outside the click handler, and nothing re-rendered. | `AfterMenu` re-renders after the action (Members, GroupEditor, Groups); regression test fails without the fix. |
| F11 | Free-a-spot confirmation read awkwardly ("Der Zugriff endet, bis Ben wieder mitmacht"). | Reworded in EN/DE/FR. |
| F12 | **Bug:** after deleting the account, its groups stayed on that device hidden for good (an owned one still opened by URL, marked *Online*). Reproduced on the phone with the older build. | New `IAccountDeletedHandler` hook (Ui.Account); Secret Santa keeps organized groups as device groups and removes groups taken part in ([FR-043](../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md) amended). Verified on the web and on the phone. Devices that deleted an account with an older build keep the hidden copy (not repaired). |
| F13 | Registration could show the raw code `PasswordRequiresUniqueChars` (an empty password fails both the length and the one-unique-character rule). Seen only through the test driver — the form itself blocks an empty password. | Mapped to the "too short" sentence, duplicates dropped; bUnit test. |
| F14 | With a large system font (1.5) the footer links wrap onto two lines and the *Gruppe erstellen* FAB covered them: the page grows to push the footer down, so the spacer inside the page never moved the footer clear of the FAB. | The FAB's room is reserved below the footer when the page has a FAB (`:has(.ct-fab)`), the in-page spacer is dropped. Verified on the phone (font 1.5) and in a narrow browser. |
| F15 | **Bug:** a second join link opened while the join page was showing (an App Link on the phone) kept showing the first group — the router reused the page and the link was read only once. | The join page re-reads the link on every navigation, as the reveal page does; bUnit test (fails without the fix). Verified on the phone. |

### Re-check on the final build (same day)

Every fix that can be seen on a phone was checked again on the last build, with a fresh account (Uma) organizing an
online group that a browser joined as Vera:

| Fix | Re-check on the S24 |
|---|---|
| F1 | ✅ no FAB on the first-run card |
| F2 | ✅ create dialog with keyboard: layout height = visible height (473/474), no pan |
| F3 | ✅ Vera's own wishlist filled + lock, "Vera pflegt das selbst." |
| F4 | ✅ last footer link ends 24px above the bottom nav after scrolling to the end |
| F5 | ✅ offline cold start lists every group (taking part and organized); reconnect + resume signs back in |
| F6 | ✅ own join link: "Du organisierst diese Gruppe"; join link of a group taken part in: "Du machst hier als Kim mit" |
| F7, F8, F9 | ✅ FR: new deletion and privacy texts dated 2026-10-25, "Introuvable", "attribution" throughout; DE "Nicht gefunden" |
| F10, F11 | ✅ new confirm wording; Vera drops from *Verbunden* at once; her browser loses the group |
| F12 | ✅ (earlier the same day) organized group kept as a device group after deleting the account |
| F14 | ✅ footer links above the FAB at normal font size too |
| F15 | ✅ two join links in a row switch the page |

### Observations (not fixed)

- O1 While a session waits for the server (F5), the header shows the signed-out avatar and no sync status.
- O2 The drawer's *Zuletzt geöffnet* lists only organized groups, not groups taken part in.
- O3 The phone has no export of device-only groups (the *Your data* section is web-only, Android auto-backup is
  off per ADR-0067): a lost phone loses them. Matches the open follow-up in
  [ADR-0087](../adr/0087-production-data-is-kept.md).
- O4 `NavMyWishlists` / `MyWishlistsTitle` look unused since *My wishlists* was folded into the group list.
- O5 The account-deleted screen says "nothing of it is left on our servers", while groups kept online for their
  participants do stay — true of the account, easy to misread.

### Not covered

Reminder delivery beyond the permission prompt, passkeys and iOS were out of scope. (The phone locked itself
mid-run; deletion and export were finished after the owner unlocked it.)

## Clustering / themes

- **Offline and session:** F5, O1.
- **Joining and membership screens:** F6, F10, F11.
- **Android layout:** F1, F2, F4.
- **Texts out of step with ADR-0086:** F7, F9, O4, O5.
- **Account deletion on the device:** F12, O3.

## Convergent — selection

All fifteen findings were fixed in the working tree (owner's instruction: fix and redeploy, no commits). The
observations are left for triage.

## Decisions & selected ideas

- Offline is not signed out: a waiting session keeps showing its account's groups and renews itself on any sync
  trigger (ADR-0021 amendment, **Proposed**).
- On the device an account was deleted from, organized groups stay as device groups (FR-043 amended).

## Implementation status

Done in the working tree, unit/bUnit tests added (refresh-token account memory, waiting-session visibility, join
page, free-a-spot re-render, account-deleted handler); `CircleTools.slnx` builds; the UI, ApiClient, LocalStore,
Main UI and Ui.Shared suites pass. Not committed.

## Rejected / parked (for the record)

- Persisting the access token to survive an offline start — rejected; ADR-0021 keeps it in memory.

## Follow-up actions

- Owner: review the legal texts (F7) and the ADR-0021 amendment; accept or change.
- Re-check F2 on the Pixel 9 Pro (Android 15+, edge to edge).
- Triage O1–O5.
