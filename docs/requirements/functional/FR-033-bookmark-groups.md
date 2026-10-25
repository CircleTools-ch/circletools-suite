---
id: FR-033
title: Bookmark groups for quick access
type: functional
status: Implemented
priority: Could
source: Stakeholder feedback (2026-08-14)
verification: test
related: [FR-023, FR-025, NFR-001, NFR-008, ADR-0041, ADR-0033, ADR-0035]
---

# FR-033 — Bookmark groups for quick access

## Description

A **signed-in** user can **pin** (bookmark) a [Group](../../glossary/group.md) so it appears as a
quick-access link in the app navigation, and **unpin** it again. Pins are a **per-account
preference** and **sync across the user's devices**.

- A pin control is shown on each group in the group list; toggling it pins or unpins that group.
- Pinned groups are listed in the sidebar navigation (and are reachable there directly), under the
  groups entry.
- Pins are **account-scoped and synced** ([FR-023](FR-023-sync-across-devices.md)): a group pinned on
  one device is pinned on the user's other devices after a sync. They ride the same offline-first
  cycle as everything else the account syncs — a pin toggled offline takes effect immediately and is
  pushed on the next sync ([NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md)).
- Pinning is a **signed-in** feature. An account-free device has no account to sync a preference to,
  so the pin control is not shown while signed out; the group list is otherwise unchanged.
- The navigation lists only pins whose group is **still held on this device**. A pin that names a
  group not present here (never on this device, or since deleted) is **omitted** rather than
  shown as a dead link; the pin is retained in case the group later arrives.

## Rationale

Once a user has more than a handful of groups, or returns to the same one repeatedly (a recurring
family exchange), scrolling the full list to reach it is friction. A pin makes the group one tap away
from anywhere in the app. Making pins **follow the account** rather than the browser means the
shortcut a user sets up on their phone is there on their laptop too — which is what "quick access"
should mean for someone who signed in precisely so their data travels with them.

Pins are deliberately **not** part of the group itself: an account-free group can be pinned, and the
same online group could be pinned differently by two organizers, so a pin is the *viewer's*
preference, not a property of the group. That is why they are stored as a per-account preference
document rather than on the aggregate ([ADR-0041](../../adr/0041-sync-secret-santa-ui-preferences.md)).

## Acceptance criteria

```gherkin
Given a signed-in user viewing their group list
When they pin a group
Then that group appears as a quick-access link in the navigation

Given a signed-in user with a pinned group
When they unpin it
Then it is removed from the quick-access navigation

Given a signed-in user who pins a group on one device
When they sync that device and another device they are signed in on
Then the group is pinned on the other device too

Given a signed-in user who pins a group while offline
When the pin is toggled
Then it takes effect immediately
And it is pushed to the account on the next sync

Given a signed-out visitor
When they view the group list
Then no pin control is shown

Given a pinned group that is not held on the current device
When the navigation is rendered
Then that pin is omitted rather than shown as a broken link
```

## Dependencies & related

- Source: stakeholder feedback (2026-08-14) — "bookmark a group in the main menu (quick access)".
- Related: [FR-023](FR-023-sync-across-devices.md) (the sync cycle pins ride),
  [FR-023](FR-023-sync-across-devices.md) (a device holds the account's online groups),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../non-functional/NFR-008-sync-convergence-and-data-safety.md)
- Governing ADRs: [ADR-0041](../../adr/0041-sync-secret-santa-ui-preferences.md) (preferences document
  and the new sync kind), [ADR-0033](../../adr/0033-which-aggregates-sync.md) (the sync catalogue this
  extends), [ADR-0035](../../adr/0035-profile-sync-seam.md) (why pins are not on the cross-app profile)
- Glossary: [Group](../../glossary/group.md), [Sync](../../glossary/sync.md)
