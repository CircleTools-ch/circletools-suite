---
status: Accepted
date: 2026-10-01
deciders: [Adrian]
tags: [ux, storage, sync, onboarding, secret-santa, privacy]
---

# ADR-0071 — Default storage mode for new groups + first-run onboarding

## Context and problem statement

[FR-038](../requirements/functional/FR-038-per-group-storage-mode.md) /
[ADR-0058](0058-account-free-link-shared-groups.md) give every group one of three storage modes —
**On this device**, **Shared by link**, **My account** — chosen per group at creation, with **"On this
device" pre-selected** as a fixed rule. That is implemented (a picker at creation, per-group switching in
Sharing & sync, a mode chip in the group detail view).

On-device testing surfaced two gaps for a new user:

1. **It is hard to tell, or decide, where a group's data lives.** The create picker is only on wide
   screens; the **phone FAB "create" bypasses it entirely and is always local**; and the group *list*
   shows no storage indicator, so after creating several groups a user cannot see at a glance which are
   private, which are link-shared, and which sync to their account.
2. **There is no "set it once" default.** Someone who wants every group on their account (or every group
   shared) must change the mode on each group, and the fixed "On this device" pre-selection means the
   phone quick-create always makes a local group regardless of intent.

**Should there be a user-level default storage mode, configured once (at first run and in Settings), that
seeds new groups — and should the group list show each group's mode?**

## Decision drivers

- **Transparency** — a user should see where each group lives, from the list, at a glance.
- **Configure once** — set the preferred storage once; don't re-decide on every group.
- **Local-first preserved** ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)) —
  anyone who does nothing still gets a private, account-free, on-device group.
- **Works signed out** — a signed-out user also creates groups and needs a sensible default; account
  mode still requires a sign-in at the point it is actually used.
- **Don't overwhelm first run** — the app already has a web local-data note (FEAT-017) and a
  getting-started checklist (FEAT-019); a storage onboarding must be once-only and skippable.

## Considered options

1. **Status quo** — keep FR-038's fixed "On this device" pre-selection; per-group only; no list
   indicator; phone FAB stays local-only.
2. **User-level default (device-local) + first-run onboarding + list indicator (chosen):** a remembered
   default storage mode seeds the create picker *and* the phone FAB; a once-only onboarding dialog lets
   the user set it (explaining the three modes and that it is per-group changeable); the group list shows
   a small per-group storage icon.
3. **Account-synced default** — store the default as an account preference so it follows the user across
   devices.

## Decision outcome

**Chosen: option 2.** A **device-local** default storage mode (`secretsanta.storage.default`, via the
same `IKeyValueStore` pattern as the other per-device preferences — reminders, recency) plus a once-only
onboarding dialog (`secretsanta.storage.onboarded`). The default **seeds both** the inline create picker
and the phone FAB quick-create, so "create" on a phone finally honours the chosen storage. The group list
gains a **per-group storage icon** (device / link / cloud), reading the mode from the sync record via the
existing `ClaimableGroupsService` (no domain change — the `SyncLayeringTests` rule stays intact).

This **refines FR-038's fixed-default rule** rather than contradicting its intent: the pre-selected value
becomes *the user's default*, which itself starts as **On this device** — so local-first still holds for
anyone who does nothing, and the three transitions/consequences of FR-038 are unchanged. When the default
is "My account" but the user is signed out, new groups fall back to **On this device** (they auto-claim
into the account on the next sync after sign-in, exactly as today) — the default never silently fails.

In the **onboarding dialog specifically**, "My account" is selectable even while signed out (unlike the
create/Settings pickers, where it stays disabled): choosing it routes the user to sign in / register, then
returns to the groups screen where the account change re-seeds the create mode to "My account". This makes
account sync reachable from the very first screen rather than a dead, greyed-out option.

Option 3 (account-synced default) is rejected: a signed-out user has no account to read the preference
from, yet still needs a default; it adds a new synced-preference surface for a device-local convenience;
and the default is really "what this device makes by default", which device-local expresses correctly.
The per-group mode and account sync of the groups themselves are unchanged — only this seed preference is
device-local.

## Consequences

- **Positive:** the storage choice is set once and honoured everywhere (including the phone FAB); the
  list makes each group's storage visible; a new user gets a plain-language explanation up front.
- **Positive:** no domain/server change — a new device-local preference, an onboarding dialog, a Settings
  control, and a list icon joined from the existing sync read model.
- **Negative / accepted:** the default is **per device**, so a user with several devices sets it on each;
  acceptable, since it is a new-group convenience and the groups themselves still sync by their own mode.
- **Negative / accepted:** one more first-run surface; mitigated by once-only + skippable, and by keeping
  it distinct from the FEAT-019 checklist (that is per-group; this is app-level, shown before the first
  group exists).
- **Follow-ups:** device-verify the onboarding + list icons on iOS/Android and narrow/wide web, DE/EN/FR;
  consider (not now) promoting the default to an account preference if multi-device users ask.
- **New risks:** low — the preference is best-effort (a read/write failure falls back to On this device),
  and nothing about the groups' actual storage depends on it beyond the pre-selected value.

## Pros and cons of the options

### 1. Status quo
- 👍 No work; FR-038 unchanged.
- 👎 Phone FAB always local regardless of intent; no "set once"; list hides where data lives.

### 2. Device-local default + onboarding + list indicator (chosen)
- 👍 Set once, honoured by every create path; transparent list; plain first-run explanation; no
  domain/server change; local-first preserved.
- 👎 Per-device default; an extra first-run surface (once-only, skippable).

### 3. Account-synced default
- 👍 Follows the user across devices.
- 👎 Useless signed out (no account to read from); new synced-preference surface; conflates a
  per-device "what I make by default" with account data.

## Links

- Refines [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md) (the fixed
  "On this device pre-selected" rule) under [ADR-0058](0058-account-free-link-shared-groups.md)
- Realised as [FR-048](../requirements/functional/FR-048-default-storage-mode-for-new-groups.md) /
  [FEAT-021](../features/FEAT-021-storage-default-onboarding-and-list-indicator.md)
- Related ADRs: [ADR-0041](0041-sync-secret-santa-ui-preferences.md) (which preferences sync vs stay
  device-local), [ADR-0015](0015-mvp-client-offline-storage.md) (local store),
  [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md) (account-scoped list)
- Related features: [FEAT-013](../features/FEAT-013-account-free-link-shared-groups.md) (the modes),
  [FEAT-017](../features/FEAT-017-local-data-transparency-and-backup.md) (web local-data note),
  [FEAT-019](../features/FEAT-019-getting-started-checklist.md) (per-group checklist)
