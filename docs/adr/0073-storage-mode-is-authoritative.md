---
status: Accepted
date: 2026-10-02
deciders: [Adrian]
tags: [storage, sync, ux, secret-santa, account]
---

# ADR-0073 — Per-group storage mode is authoritative (On this device stays local when signed in)

## Context and problem statement

[FR-038](../requirements/functional/FR-038-per-group-storage-mode.md) /
[ADR-0058](0058-account-free-link-shared-groups.md) promise three real, per-group storage modes — **On
this device**, **Shared by link**, **My account** — and [ADR-0071](0071-default-storage-mode-and-first-run-onboarding.md)
surfaced the mode prominently (a default + a per-group list icon). On-device use revealed the modes were
**not actually honoured when signed in**:

- `Groups.CreateNamedAsync` only acted on *Shared by link*; both *On this device* and *My account* were
  stored with the record's default `Mode = OnThisDevice`.
- `AutoSyncService.RunCycleAsync` then **auto-claimed every `Mode == OnThisDevice`** account-free group
  into the account.

So while signed in, **both "On this device" and "My account" ended up as account-synced groups** — only
"Shared by link" was distinct. A user who chose "On this device" while signed in watched it become an
account group. This also collides with the sign-in reconciler (ADR-0044): its "Cancel = keep my groups
local" choice was undone by the same auto-claim.

**Should the chosen storage mode be authoritative — in particular, should "On this device" stay local
even when signed in?**

## Decision drivers

- **Modes must mean what they say** — "On this device" must stay on the device; "My account" must sync.
  Otherwise the picker, the default, and the list icon are all lying.
- **Local-first** ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)) — a
  signed-in user keeping a group private is a first-class case, not a transient pre-account state.
- **Coherence with sign-in reconciliation** (ADR-0044) — Cancel/keep-local must actually keep local.
- **Minimal, testable change** to a sensitive sync path.

## Considered options

1. **Keep today's behaviour** (signed-in syncs everything) and just relabel the UI — the modes stay
   not-quite-real.
2. **Drop "On this device" when signed in** — offer only My account / Shared by link while signed in.
3. **Make `Mode` authoritative (chosen):** "On this device" stays local even when signed in; "My account"
   is recorded on create and synced; auto-claim targets only `Mode == Account`.

## Decision outcome

**Chosen: option 3.** `Mode` is the per-group intent and is honoured:

- `SyncService.ClaimAsync` sets `Mode = Account` on the record it scopes (the one place a group actually
  becomes account-owned — so the claim path, the auto-claim, and the sign-in reconciler's Import all end
  with the mode reflecting reality).
- `SyncService.MarkForAccountAsync(groupId)` (new) records "My account" intent locally — sets
  `Mode = Account` on a plain local group — so the next sync cycle claims it (offline-safe: it marks
  intent rather than calling the server).
- `AutoSyncService.RunCycleAsync` now auto-claims only `Mode == Account` account-free groups. **A
  `Mode == OnThisDevice` group is never auto-claimed**, so it stays local while signed in.
- `Groups.CreateNamedAsync` calls `MarkForAccountAsync` for the "My account" choice (previously a no-op
  that relied on claim-everything); "On this device" does nothing and stays local; "Shared by link" is
  unchanged.

This pairs with the ADR-0044 amendment (the sign-in reconciler's Cancel keeps groups local): Cancel +
"On this device stays local" now agree — a group the user keeps local is genuinely local.

**Create UX follow-up (same decision, separate change):** the two configuration points (an always-open
radio picker inline on wide screens + a silent default on the phone FAB) are the confusing part. Replace
both with **one compact affordance at create, phone and web alike**: show how the group will be stored as
the same icon used in the list, with a one-tap "Change" for this group. The default (onboarding/Settings)
stays the single source of truth; create just shows it. Tracked for implementation after this model fix
lands and is verified on device.

## Consequences

- **Positive:** the three modes are real and stable; the list/create icon is truthful; "keep local while
  signed in" works; the reconciler's Cancel is coherent.
- **Behaviour change:** a signed-in device no longer force-claims every local group. Existing un-claimed
  local groups on a signed-in device now stay local; the way to move them onto the account is the
  explicit sign-in **Import** or the per-group Sharing & sync screen. Groups already claimed (AccountScope
  set) are unaffected; their stale `Mode` reads correctly from `AccountScope` in the list icon.
- **Negative / accepted:** "My account" created offline is claimed on the next online cycle rather than
  instantly — the group shows as My account immediately (mode set), the upload follows.
- **Follow-ups:**
  1. The compact create affordance (icon + Change), phone + web.
  2. A sync test asserting "On this device stays local when signed in" and "My account auto-claims" — the
     old auto-claim filter flipped with no test catching it.
  3. Consider backfilling `Mode = Account` for already-claimed records (cosmetic; the icon already derives
     it from `AccountScope`).

## Pros and cons of the options

### 1. Keep behaviour, relabel UI
- 👍 Smallest change.
- 👎 The modes remain not-real; the bug (local → account) stands.

### 2. Drop "On this device" when signed in
- 👍 Fewer choices; no "why didn't it stay local" surprise.
- 👎 Removes a legitimate private-local option for signed-in users; contradicts FR-038.

### 3. Mode authoritative (chosen)
- 👍 Modes mean what they say; local-first honoured; coherent with ADR-0044.
- 👎 Changes a sensitive sync path (mitigated: small, tested, reversible).

## Links

- Refines [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md) /
  [ADR-0058](0058-account-free-link-shared-groups.md); builds on
  [ADR-0071](0071-default-storage-mode-and-first-run-onboarding.md) /
  [FEAT-021](../features/FEAT-021-storage-default-onboarding-and-list-indicator.md)
- Amends the auto-claim behaviour of [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md)
  (which also had its Cancel path corrected on 2026-10-02)
