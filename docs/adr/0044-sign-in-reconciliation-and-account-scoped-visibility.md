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
  - **Cancel** — sign back out; nothing imported, nothing deleted.

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
