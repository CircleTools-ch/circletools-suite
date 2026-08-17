---
status: Accepted
date: 2026-08-14
deciders: [Adrian]
tags: [architecture, sync, offline, secret-santa, ui]
---

# ADR-0041 — Sync Secret-Santa UI preferences as an opaque sync-store kind (extending ADR-0033)

## Context and problem statement

Users can now **pin a group** for quick access from the navigation
([FR-033](../requirements/functional/FR-033-bookmark-groups.md)), and asked for those pins to **follow
them across their devices**, not live on one browser. A pin is a *per-account UI preference* — it is
not part of the [Group](../glossary/group.md) aggregate (an account-free group can be pinned; two
people organizing the same claimed group may pin different things), and it is **Secret-Santa-specific**
(it names Secret-Santa group ids).

[ADR-0033](0033-which-aggregates-sync.md) fixed the M3 sync catalogue at exactly two units: the claimed
**Group** and **profile preferences** (display name, language). Pinned groups are a *third* kind of
per-account state that now wants to sync. **Where does it live, and does it need a new server-side
store?**

## Decision drivers

- **Pins are per-account, single-owner state — safe under last-write-wins.** Like a group and unlike a
  circle, one account owns its own pins; discarding the losing side of a genuine collision loses only
  *that user's own* edit, the bounded cost ADR-0016 already accepts. The reasons ADR-0033 kept circles
  out of sync (shared data, deferred server-side authorization) **do not apply** here.
- **Keep the cross-app profile app-neutral.** The profile document is authoritative on the Main
  service's `/account/profile` and is read by the Main app too (ADR-0035). Stuffing Secret-Santa group
  ids into it couples one app's UI state into a shared, cross-app record. Pins should stay inside the
  Secret-Santa boundary.
- **The M3 sync store is already kind-agnostic.** Platform stores every synced aggregate as an
  **opaque** JSON body tagged by a `kind` string and never deserializes it (ADR-0016, ADR-0033); the
  client engine already carries an unknown kind through untouched, explicitly "left for whoever adds
  it". A new kind therefore needs **no server schema, endpoint, or migration** — only client code.
- **Offline-first, like every other Secret-Santa write.** A pin toggled offline must take effect at
  once and reconcile later (NFR-001, NFR-008), which is exactly what the group sync cycle already does.

## Considered options

1. **A new Secret-Santa-scoped "preferences" aggregate that rides the existing opaque M3 sync store** as
   its own `kind`, wrapped in the same version/dirty/account-scope metadata a group carries.
2. **Extend the Main profile document** with the pinned group ids, syncing them through the ADR-0035
   profile seam.
3. **Device-local only** — store pins in this browser's `localStorage` and do not sync them.

## Decision outcome

**Chosen: Option 1.** Pinned groups become a per-account **preferences document** — a pristine body
(`PreferencesState { pinnedGroupIds }`) wrapped locally in a `SyncedPreferencesRecord`
(version, account scope, dirty) exactly as a group's memento is wrapped in a `SyncedGroupRecord`. It
syncs through the **existing** M3 store under a new `SyncKinds.Preferences` (`"preferences"`) kind, with
the account id as its aggregate id (one document per account). ADR-0016's mechanism — aggregate as the
unit, version token, cursor, last-write-wins by server time — applies unchanged.

This **extends ADR-0033's catalogue** from *Group + profile* to *Group + profile + Secret-Santa UI
preferences*. It is an extension, not a reversal: ADR-0033's exclusion of **circles** stands for the
same reasons (shared data, deferred authorization), none of which a single-account UI preference
triggers. ADR-0033 carries an "extended by ADR-0041" note.

Because the sync store is opaque and kind-agnostic, **no Main-service change is required**: the server
stores and echoes the `preferences` body like any other aggregate. The client `SyncService` gains a
focused push/pull for the document, run inside the same cycle and **before the cursor advances**, so a
storage failure re-fetches rather than skips.

Option 2 is rejected for coupling one app's UI state into the cross-app profile. Option 3 is rejected
because the user explicitly asked for pins to follow them across devices; device-local would not.

## Consequences

- **Positive:** pins sync across a signed-in account's devices with **no server schema, endpoint, or
  migration** — the opaque sync store carries the new kind for free, which is exactly what its kind
  string was for.
- **Positive:** the Group aggregate and the cross-app profile both stay clean — pins touch neither.
- **Negative / accepted:** pins are a **signed-in** feature. An account-free device has no account to
  sync to, so the pin control is shown only while signed in; account-free users keep the (unchanged)
  group list. This matches every other synced surface.
- **Negative / accepted:** a pin can name a group not held on a given device (never claimed there, or
  since deleted). The navigation simply **omits** pins with no matching local group rather than showing
  a dead link; the id is kept in case the group arrives later.
- **Follow-ups:** [FR-033](../requirements/functional/FR-033-bookmark-groups.md); this ADR; the
  `SyncKinds.Preferences` kind and the `SyncService` preferences reconcile. No glossary term is added —
  "pin/bookmark" is UI vocabulary, not a domain term.
- **New risks:** the preferences document uses the **account id as its aggregate id**, so it shares the
  sync store's `(account, id)` primary key space with groups. A group whose client-minted GuidV7 id
  equalled the account id would collide; the probability is negligible, and the two are distinguished by
  `kind` everywhere the client reads them. Worth noting, not worth a mitigation.

## Pros and cons of the options

### 1. New Secret-Santa preferences aggregate on the opaque sync store (chosen)
- 👍 No server change of any kind; reuses the built, tested M3 store and its offline/LWW guarantees.
- 👍 Keeps pins inside the Secret-Santa boundary; Group aggregate and cross-app profile untouched.
- 👎 Adds a focused branch to the client `SyncService` (a second kind in the cycle).

### 2. Put pinned group ids on the Main profile document
- 👍 Reuses the ADR-0035 profile seam; no new kind.
- 👎 Couples Secret-Santa UI state into a shared, cross-app record the Main app also reads.

### 3. Device-local only (`localStorage`, no sync)
- 👍 Simplest; no sync surface at all.
- 👎 Does not meet the request — pins would not follow the user to another device.

## Links

- Related ADRs: [ADR-0033](0033-which-aggregates-sync.md) (**extended by this ADR**),
  [ADR-0016](0016-sync-and-conflict-resolution.md) (the mechanism reused),
  [ADR-0035](0035-profile-sync-seam.md) (the profile seam kept app-neutral),
  [ADR-0020](0020-server-side-assignment-privacy.md)
- Requirements: [FR-033](../requirements/functional/FR-033-bookmark-groups.md),
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Feature: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) (the sync engine extended)
- Glossary: [Sync](../glossary/sync.md), [Group](../glossary/group.md)
