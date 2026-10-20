---
status: Accepted
date: 2026-08-14
deciders: [Adrian]
tags: [architecture, sync, offline, secret-santa, ui, account]
---

# ADR-0044 — Sign-in reconciliation of offline data and account-scoped visibility

## Context and problem statement

The M3 sync model ([FEAT-004](../features/FEAT-004-m3-sync-and-claim.md)) made a signed-in account's
data sync, but the **offline↔online transitions** it exposes confuse users:

- **Signing in does nothing.** A [Group](../glossary/group.md) created offline stays *account-free*.
  To get it onto the account the user must find the separate **`/claim`** screen, tick boxes, confirm,
  then press **"Sync now"** by hand. Nothing prompts them, so signing in looks like it did nothing.
- **Signing out keeps everything on screen.** Account data stays fully visible offline; only a badge
  changes. There is no "this is my account now, it lives in the cloud" model.
- **Sync is manual.** Despite [FR-024](../requirements/functional/FR-024-offline-editing-and-reconciliation.md)
  specifying reconnect sync "without the user asking", the shipped client only syncs on the manual
  button — there is no connectivity trigger.

**What should the offline/online model feel like, and how do offline creations reconcile with an
account at the moment of sign-in?**

## Decision drivers

- **The expected consumer-app mental model.** Work freely offline; on sign-in, your offline things
  come with you (or you drop them) and then everything is "in your account"; signed out, account data
  is not shown. This is what people already expect from note/photo apps.
- **Offline-first, invisibly.** Every edit must work with no connection and reconcile silently on
  reconnect (NFR-001, NFR-008, and FR-024's own acceptance criteria) — no modal, no spinner that
  blocks editing, on both the web and the MAUI head.
- **Data safety.** A user must never silently lose offline work. The choice to import or discard is
  **theirs**, made explicitly, and hiding-on-logout must not delete anything.
- **Reuse the built engine.** `SyncService`'s dirty-queue / claim / delta-reconcile mechanics already
  exist and are safe across interrupted cycles; this should be **wiring and UX**, not a new engine.

## Considered options

1. **Keep the manual model** (explicit `/claim` screen + manual "Sync now"), maybe with nudges.
2. **Auto-claim everything silently on sign-in**, no prompt; hide account data when signed out.
3. **Prompt once at sign-in — Import / Discard / Cancel — then sync automatically thereafter**
   (sign-in, app start, every change, reconnect); hide (not delete) account data when signed out.

## Decision outcome

**Chosen: Option 3.**

- **At the anonymous→authenticated transition** (any entry path — password, passkey, cross-app SSO
  callback, registration), if the device holds any **account-free** groups, the app shows one dialog:
  - **Import** — claim every account-free group into the account, then a full sync (push + pull).
  - **Discard** — delete every account-free group locally, then sync (pulls the account's data).
  - **Cancel** — nothing imported, nothing deleted; the sign-in **stays**. *(Amended 2026-10-02: Cancel
    originally signed the user back out via `SignOutAsync`, which calls `POST /logout` → `RevokeAll` and
    revokes **every** session on **every** device. Worse, when the prompt failed to display,
    `ShowMessageBoxAsync` returned `null`, which the code treated as Cancel — so a sign-in with local
    groups silently logged the user out everywhere. A non-choice must never end the session: Cancel/dismiss
    now keeps the sign-in and leaves the local groups device-only, importable later per group. The explicit
    account-menu "Sign out" is unchanged.)*

  With **no** account-free groups, no dialog appears — just a background sync. A **restored** session
  (silent startup refresh, [ADR-0021](0021-web-auth-token-model.md)) is **not** prompted: the
  prompt is only for an interactive sign-in observed while the app is running.

- **Sync becomes automatic and silent**, driven by four triggers — **sign-in**, **app start while
  signed in**, **after every change** (a debounced request fired from the CQRS pipeline), and
  **reconnect** (a new `IConnectivity` seam). Failures are swallowed and retried on the next trigger;
  the dirty queue survives, so nothing is lost. The manual "Sync now" button stays as an explicit retry.

- **Account data is account-scoped in the UI.** Signed out, the group list shows **only** account-free
  groups; account groups are **hidden but kept** on the device (they reappear instantly on the next
  sign-in, no re-download). Hiding is a read-model filter — the local copy is never deleted, so data
  safety (NFR-001/NFR-008) is untouched.

- **Groups created while signed in are claimed on creation**, so a signed-in device never accumulates
  account-free groups and the signed-in list is always fully synced.

Because a signed-in device holds no account-free groups, the standalone **`/claim` screen and the
un-claim affordance become dead** and are removed: importing is the sign-in dialog, and "remove from
the account" folds into **delete** (which already tombstones across devices, FR-024). The
`SyncService.ClaimAsync`/`UnclaimAsync` engine methods remain — the dialog uses `ClaimAsync`.

Option 1 is the status quo being fixed. Option 2 is rejected because silently absorbing a user's
offline data into whatever account happens to sign in — possibly a shared device, possibly the wrong
account — is exactly the data-ownership surprise the explicit prompt prevents.

## Consequences

- **Positive:** the transitions match user expectation — sign in and your things are there and synced;
  sign out and account data is not shown. Sync is invisible and offline editing "just works" on both
  heads.
- **Positive:** net UI removed (claim screen, un-claim flow, "Synced" badge) — the model is simpler,
  not just different.
- **Negative / accepted:** once signed in, a group can no longer be kept deliberately *local-only* on
  that device — creating while signed in claims it. This is the intended model ("everything is in my
  account when I'm logged in"), not a regression.
- **Negative / accepted:** hide-not-delete means account data physically remains in `localStorage`
  after logout. On a shared device it is not *shown*, but it is not *erased*; a future "sign out and
  wipe this device" is a separate opt-in, out of scope here.
- **Follow-ups:** the `IConnectivity` seam (web + MAUI), an `AutoSyncService`, a `SignInReconciler`
  dialog, a `VisibleGroupsService` read filter, a post-command sync pipeline behavior; doc updates to
  FR-024/FR-025/FR-026, NFR-001, and the affected UCs. Reverses the manual halves of
  [FR-025](../requirements/functional/FR-025-claim-local-groups.md) and
  [FR-026](../requirements/functional/FR-026-remove-group-from-account.md).
- **New risks:** the auto-sync-after-change hook could recurse if a sync's own writes re-triggered it;
  mitigated by a reentrancy guard in `AutoSyncService` and by firing only on user-dispatched commands.

### Amendment (2026-08-17) — the manual "Sync now" button is removed

The decision above kept a manual **"Sync now"** button "as an explicit retry" alongside the silent
auto-sync. In practice the four automatic triggers (sign-in, app-start, every change, reconnect)
cover every case, so the button was redundant and its only remaining value was *surfacing* the sync
outcome. It is now removed from the shell. To keep that feedback, `AutoSyncService` exposes its last
group-cycle outcome (`LastStatus`) and its `SyncCompleted` event; the drawer shows this in a passive
status line (moved to the app bar on 2026-10-16, see below). This completes the "sync is invisible" direction of this ADR rather than reversing it.

### Amendment (2026-08-17) — one-click self-link, and send-time duplicate refusal

Linking a member to the signed-in account had only one path on the roster: the **"Link…"** box, where
you typed an address. To become a member yourself you had to type *your own* email — and if it was not
yet verified the request silently went nowhere (the server matches verified addresses only), which is
the confusing case the sign-in banner was added to explain. Two changes make this direct:

- **A one-click "Link with my account".** On the members roster, each unlinked member offers a
  **"Link with my account"** button that self-links with **no address and no email-confirmation step**
  (it posts to `member-links/self`, which takes the account from the bearer token, FR-032). The client
  sets `Member.UserId` locally at once and the ordinary auto-sync propagates it; the server's own
  resolution re-applies idempotently. Because **one account backs at most one member per group**, the
  button is shown **only while my account backs no member of this group** — once I am a member anywhere
  in the group it disappears everywhere, leaving only the invite-an-address path.

- **Duplicate links are refused at send time, not at accept time.** Previously a second invitation to an
  address that already backed a member was queued and only rejected when the recipient tried to *accept*
  (`AlreadyLinked`); the organizer got no feedback. `SendMemberLink` now resolves the address and, if it
  already backs a **different** member of that group — **linked, or with a live pending invitation** —
  returns a new `MemberLinkOutcome.AlreadyLinkedInGroup` the roster shows as an error. Re-inviting the
  **same** member at the same address is a resend, not a duplicate, and is allowed. This also rejects an
  organizer trying to self-link twice.

  This is a **deliberate, scoped relaxation of NFR-005's constant-time guarantee**: the refusal only ever
  fires for an account already linked to (or invited into) the caller's **own** group, whose linked
  members the caller can already see (the "linked" badges). An **arbitrary** address still returns the
  identical `RequestSent`, so the surface still cannot be used to probe whether a given address has an
  account — the enumeration protection the original decision cares about is intact. The narrow new
  disclosure (that a *specific* address the organizer typed maps to one of their *own* already-linked
  members) was judged an acceptable trade for immediate, honest feedback on a duplicate.

### Amendment (2026-10-14) — visibility is per account; a "keep local" answer is remembered

Found while testing sign-in / sign-out on the
[2026-10-14 UX review](../brainstorming/2026-10-14-secret-santa-android-ux-review.md):

- **Signed in, only the current account's groups are shown** (plus the account-free ones), not "all
  groups". A group claimed into a *different* account and still cached on the device (a shared device,
  or an earlier sign-in) used to appear for whoever signed in next, labelled "My account". It stays
  cached, as on sign-out, but is hidden until its own account signs in again (`VisibleGroupsService`).
- **The reconciliation prompt does not re-ask about groups already decided.** Answering "Cancel" (keep
  local) used to bring the same prompt back on every sign-in. Worse, it also counted groups deliberately
  created "On this device" *while signed in* as "created while signed out". Both decisions are now
  remembered device-locally (`KeptLocalGroups`), and the prompt lists only undecided account-free groups.
  Moving a kept group into the account later is still one tap in Sharing & sync.

### Amendment (2026-10-15) — the prompt is a sheet that must be answered; sync also runs on resume

From the [2026-10-15 sync test](../brainstorming/2026-10-15-secret-santa-sync-device-test.md) (S1, S7, D2,
D3):

- **The import prompt never showed on Android (S1).** It was the app's only MudBlazor message box. Every
  other confirmation is a `ConfirmSheet` and works on the phone. A message box that never appeared returned
  "no answer", and the reconciler took that as **Cancel**: it silently kept the groups local and remembered
  that, so they were never offered again. Now:
  - the prompt is a **bottom sheet**, the same component family as every other confirmation, with the three
    answers Import / Discard / Keep on this device. It can't be dismissed without one of them;
  - **only an explicit "Keep on this device" is remembered.** If the prompt is closed with no answer (the
    app is closed, or the sheet couldn't be shown), nothing is decided: the groups stay local and the
    prompt asks again at the next sign-in.
- **The prompt speaks the account's language (D3).** Signing in switches the UI to the account's language.
  The prompt is now built after that switch, so it no longer appears in the previous language.
- **The list follows an import immediately (D2).** After Import or Discard, the groups list refreshes
  without a reload.
- **Sync triggers (S7).** The original four triggers (sign-in, app start, every change, reconnect) never
  fire while the app is just open, or when it comes back from the background. Changes made on another
  device only arrived after pull-to-refresh. Two triggers are added:
  - **resume:** coming back to the foreground (MAUI window resumed; on the web, the page becoming visible
    again) requests a sync;
  - **foreground poll:** while the app is in the foreground, a sync is requested every **2 minutes**. A
    background app never polls. A no-op cycle carries no bodies (NFR-008), so the cost is one small
    request per account and one per shared group.

### Amendment (2026-10-16) — the prompt names the groups and has no discard

From the [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
(Q35): the prompt said "2 groups" without naming them, offered one answer for all, and its red **Discard**
deleted every listed group irreversibly — at the moment people pay least attention (just signed in), and
one of them held a draw. Now:

- The prompt **lists each group by name** with a short summary ("4 members · drawn").
- Every group is **ticked** by default. **Import** moves the ticked ones into the account; **unticked ones
  stay on this device** and are remembered as kept (not asked again).
- **Later** decides nothing; the prompt asks again at the next sign-in (as an unanswered prompt does today).
- **No discard** in the prompt. Deleting stays where it is — the group's menu, with its own confirmation.

### Amendment (2026-10-16) — the sync status moves to the app bar as an icon

From the same test (Q14, T3): the drawer's status line ("Synchronisiert – alles auf dem neusten Stand")
wrapped badly and read unclearly. The passive status now sits in the **app bar, beside the account button**,
and only while signed in:

- **Syncing:** a small spinner, nothing else.
- **Synced:** a cloud-check icon only; hover or tap shows "Synced successfully – 2 min ago".
- **Offline / failed / storage full:** the icon plus a short word ("Offline", "Not synced", "Storage
  full"). Tapping it opens the detail — what it means, when the last sync succeeded and, unless storage is
  full, **"Try again now"**. Offline shows as soon as the connection drops, not only after a failed attempt.

`AutoSyncService` adds `IsSyncing`, `LastSyncedAt`, `SyncStarted` and `StatusChanged` for this. Still no
manual "Sync now" in normal use: the retry appears only when something went wrong.

### Amendment (2026-10-20) — a shared group stays shared when imported; joined groups are not offered

From the [2026-10-20 release device test](../brainstorming/2026-10-20-secret-santa-release-device-test.md) (R11): the import uploaded every ticked group to the account
store. A group *shared by link* while signed out thereby became a private *My account* group — its join and
management links disappeared and the people who had joined lost it. Now:

- A ticked **shared-by-link** group is claimed through its management link, as *Sharing & sync → Keep in my
  account* does, so it becomes *My account · shared* and its join link keeps working
  ([ADR-0081](0081-signed-in-sharing-stays-in-the-account.md)). Plain local groups still go through the
  account store.
- A group this device only **joined** by link is the organizer's and is **not listed** in the prompt.
- As a safety net the account-store claim (`SyncService.ClaimAsync`) skips shared-by-link groups.

## Pros and cons of the options

### 1. Keep the manual model
- 👍 No change; the engine already supports it.
- 👎 It is the exact behaviour reported as broken — invisible claim screen, manual sync, data lingers.

### 2. Auto-claim everything silently on sign-in
- 👍 Zero friction; no dialog.
- 👎 Silently binds a user's offline work to whichever account signs in — wrong on a shared device, and
  no way to discard instead. A data-ownership surprise.

### 3. Prompt once (Import/Discard/Cancel), then auto-sync; hide-on-logout (chosen)
- 👍 Matches the expected model; the user owns the import/discard/cancel choice; sync is invisible after.
- 👎 One dialog on first sign-in with offline data; retires the claim/un-claim surfaces (net simpler).

## Links

- Related ADRs: [ADR-0016](0016-sync-and-conflict-resolution.md) (mechanism reused),
  [ADR-0021](0021-web-auth-token-model.md) (in-memory token → restore is silent),
  [ADR-0025](0025-shared-ui-building-blocks.md) /
  [ADR-0028](0028-browser-platform-adapters-block.md) (the seam pattern the `IConnectivity` seam
  follows), [ADR-0034](0034-shared-account-session-ui-block.md) (shared shell hosts the
  interception), [ADR-0039](0039-cross-app-single-sign-on.md) (SSO is one covered entry path),
  [ADR-0041](0041-sync-secret-santa-ui-preferences.md)
- Requirements: [FR-024](../requirements/functional/FR-024-offline-editing-and-reconciliation.md),
  [FR-025](../requirements/functional/FR-025-claim-local-groups.md) (**superseded UX**),
  [FR-026](../requirements/functional/FR-026-remove-group-from-account.md) (**retired**),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Feature: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) (the sync engine wired to auto-triggers)
- Glossary: [Sync](../glossary/sync.md), [Group](../glossary/group.md)
