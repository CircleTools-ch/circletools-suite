---
id: FR-048
title: A user-level default storage mode seeds new groups, set at first run and in Settings
type: functional
status: Approved
priority: Should
source: UC-SS-017
verification: test
related: [FR-038, UC-SS-017, ADR-0071, ADR-0058, NFR-001, FEAT-021]
---

# FR-048 — A user-level default storage mode seeds new groups, set at first run and in Settings

## Description

The app remembers a **default [storage mode](../../glossary/shared-by-link-group.md)** for new
[groups](../../glossary/group.md) — one of the three modes of
[FR-038](FR-038-per-group-storage-mode.md) (**On this device** / **Shared by link** / **My account**).
The default is **device-local** ([ADR-0071](../../adr/0071-default-storage-mode-and-first-run-onboarding.md))
and:

- **Starts as "On this device"** until the user changes it, so local-first
  ([NFR-001](../non-functional/NFR-001-local-first-offline.md)) holds for anyone who does nothing.
- **Is set at first run** via a one-time, skippable onboarding dialog that explains the three modes and
  **states that the storage can still be changed per group** (FR-038). In this onboarding dialog
  **"My account" is selectable even while signed out** (unlike the create/Settings pickers, where it is
  disabled): choosing it and saving **takes the user to sign in / register**, after which new groups
  default to "My account".
- **Is changeable anytime** from **Settings** ("Default storage for new groups").
- **Seeds every create path** — both the inline create picker (which pre-selects the default instead of a
  hard-coded "On this device") *and* the phone **"+" quick-create**, which previously always created a
  local group.
- **Falls back to "On this device" when the default is "My account" but no session is live**, since there
  is no account to attach to yet; the group auto-claims into the account on the next sync after sign-in,
  exactly as a locally-created group does today ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)).

Separately, the **group list shows each group's current storage mode** as a small icon (device / link /
cloud), read from the sync record, so a user can see at a glance where each group lives.

This requirement **refines the fixed pre-selection rule in [FR-038](FR-038-per-group-storage-mode.md)**
(the pre-selected value is now the user's default) without changing FR-038's modes, transitions, or
consequence prompts.

## Rationale

Users could not tell where a group's data lived, and had to re-choose storage on every group — while the
phone quick-create silently ignored the choice and always went local. A remembered default set once, a
plain first-run explanation, and a per-group storage icon make the storage model transparent and
"configure once", while keeping the private, account-free default intact for anyone who does nothing
([ADR-0071](../../adr/0071-default-storage-mode-and-first-run-onboarding.md)).

## Acceptance criteria

```gherkin
Given a fresh install where the storage onboarding has not been shown
When the user first opens the groups screen
Then a one-time dialog explains the three storage modes, notes it is changeable per group, and lets the user set a default (or skip)

Given the onboarding dialog was shown or skipped once
When the user opens the groups screen again
Then the dialog is not shown again

Given the user has set the default storage mode to "Shared by link"
When the user creates a group from the phone "+" quick-create
Then the new group is created in "Shared by link" mode (not "On this device")

Given the first-run onboarding and the user is signed out
When the user selects "My account" and saves
Then they are taken to sign in / register, and after signing in new groups default to "My account"

Given the user has set the default to "My account" and is signed out
When the user creates a group
Then the group is created "On this device" and will auto-claim into the account after the next sign-in

Given the groups list with groups in different modes
When it is shown
Then each group row shows a storage icon indicating On this device, Shared by link, or My account

Given the Settings screen
When the user changes "Default storage for new groups"
Then the new default is remembered and pre-selected the next time a group is created

Given a user who never changes the default
When they create a group
Then it is "On this device" (local-first default preserved — FR-038 / NFR-001)
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md)
- Refines: [FR-038](FR-038-per-group-storage-mode.md) (the fixed "On this device" pre-selection)
- Governing ADR: [ADR-0071](../../adr/0071-default-storage-mode-and-first-run-onboarding.md); builds on
  [ADR-0058](../../adr/0058-account-free-link-shared-groups.md) (the modes) and
  [ADR-0041](../../adr/0041-sync-secret-santa-ui-preferences.md) (device-local vs synced preferences)
- Implemented by: [FEAT-021](../../features/FEAT-021-storage-default-onboarding-and-list-indicator.md)
- Related: [NFR-001](../non-functional/NFR-001-local-first-offline.md) (default preserved)
- Glossary: [Group](../../glossary/group.md),
  [Shared-by-link group](../../glossary/shared-by-link-group.md)
