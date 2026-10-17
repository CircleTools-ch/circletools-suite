---
id: FEAT-025
title: Storage model & onboarding — owned shared groups, create-time storage choice, named import prompt
status: Done
owner: Adrian
target-milestone: Next release
bounded-context: SecretSanta (client) + Main (shared-group API)
related: [ADR-0081, ADR-0071, ADR-0044, ADR-0058, ADR-0078, FR-038, FR-048, TP-025]
---

# FEAT-025 — Storage model & onboarding

## Problem & goals

- **Problem:** the [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
  found that a *My account* group couldn't be shared and sharing a group moved it out of the account (Q31),
  that the sharing prompts used jargon and wrong words (Q15), that a storage modal greeted every first visit
  (Q24) and that the sign-in import prompt named no groups and could discard all of them (Q35).
- **Goals:** a signed-in organizer's shared group stays in their account; the storage choice is explained
  where a group is created; the import prompt names the groups and can't delete them.
- **Non-goals:** co-organizing with an owned group (it still needs "hand over without an account").

## Requirements & use cases covered

| Item | Covered? | Notes |
|------|----------|-------|
| [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md) | ✅ | transitions per ADR-0081 |
| [FR-048](../requirements/functional/FR-048-default-storage-mode-for-new-groups.md) | ✅ | amended: no first-run dialog |
| Q15, Q24, Q31, Q35 | ✅ | backlog WP-2 |

## Design overview

Per [ADR-0081](../adr/0081-signed-in-sharing-stays-in-the-account.md), the
[ADR-0071](../adr/0071-default-storage-mode-and-first-run-onboarding.md) and
[ADR-0044](../adr/0044-sign-in-reconciliation-and-account-scoped-visibility.md) amendments of 2026-10-16:

- **Choices.** Signed out: On this device / Shared by link / My account (sign-in needed). Signed in: On this
  device / My account / My account · shared. `StorageModeDisplay.Choices` and a display `StorageKind`
  (device, link, account, account-shared) drive the picker, the create field, the list chip and the header
  chip.
- **Owned share.** `SharedSyncService.ShareOwnedAsync` = share + claim-admin. "Invite people" from *My
  account* = leave the account store + owned share. "Stop sharing" = `StopSharingAsync` (owner delete) + mark
  for the account store. A link-only group offers "Keep in my account". "Hand over without an account" =
  release-to-link. The owner frees a joined person with `DELETE /shared/{id}/owned/claims/{memberId}`.
- **Other devices.** After an account cycle that dropped a group, owned-group discovery runs again, so an
  invited group reappears in the same cycle.
- **Create.** No onboarding dialog; the first create shows the options expanded with explanations
  (`StorageModeField.Expanded`); signed out, "My account" routes through sign-in back to the create dialog
  (`?create=account`).
- **Import prompt.** `ReconcileSheet` lists groups with a summary, all ticked; Import / Later; no discard.

## API / contract changes

`DELETE /shared/{groupId}/owned/claims/{memberId}` (bearer, owner only) — new.

## Data & migration considerations

None (no schema change). Device-local `secretsanta.storage.onboarded` now means "storage explained once".

## Localization impact

New and reworded strings in DE/EN/FR (account-shared labels, invite / stop-sharing / hand-over prompts,
management link, import prompt). "Admin link" no longer appears in product text.

## Testing strategy

See [TP-025](TP-025-storage-model-and-onboarding.md).

## Rollout / deployment & feature flags

Server first (owner release endpoint), then the apps. No flag.

## Risks & open questions

- Share + claim are two calls: offline in between leaves a link-only group, shown with "Keep in my account".
- The E2E suite (`tests/E2E`) drives the old onboarding/picker flow in places and must be re-run and adapted.

## Definition of Done

- [x] Meets acceptance criteria of all covered items
- [x] Builds clean under analyzers (warnings = errors)
- [x] Tests per TP-025 pass (unit, component, store)
- [x] Docs and localized strings
- [x] Browser walkthrough (two devices of one account)
- [ ] E2E suite re-run and adapted; Pixel check
