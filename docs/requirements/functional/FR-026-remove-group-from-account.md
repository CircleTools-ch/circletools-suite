---
id: FR-026
title: Remove a group from the account
type: functional
status: Implemented
priority: Should
source: UC-SS-008
verification: test
related: [UC-SS-008, UC-SS-007, UC-MAIN-015, FR-023, FR-025, FR-015, NFR-002, ADR-0017, ADR-0022, FEAT-004]
---

# FR-026 — Remove a group from the account

## Description

A user must be able to take a claimed [Group](../../glossary/group.md) **back out of their account**
without deleting the group and without deleting their account.

- The server copy is deleted — members, [wishlists](../../glossary/wishlist.md), and the
  [Draw](../../glossary/draw.md) with its [assignments](../../glossary/assignment.md) if one has run.
- The group **stops syncing** and becomes an ordinary account-free group on the device that performed
  the action.
- The **local copy stays**, on this device and on every other device that already held it. Those
  copies freeze at the last synced version.
- The confirmation distinguishes this from **deleting** the group, in words: deleting removes it
  everywhere via a [tombstone](../../glossary/tombstone.md); removing from the account removes it from
  the server only.
- Already-shared [reveal links](../../glossary/reveal-link.md) are unaffected.
- The group may be claimed again later ([FR-025](FR-025-claim-local-groups.md)); the claiming device's
  copy is what lands.
- The action requires a connection. Offline, it is unavailable and says why.

## Rationale

Without a way back out, [claiming](../../glossary/claim.md) is a one-way door, and people do not walk
through one-way doors with their family's data. ADR-0017 states the property this requirement
implements: *a claimed group can be removed from the account, which leaves the local copy intact*.

Leaving the other devices' local copies alone is the point of the whole design and the reason this is
**not** the same signal as a delete. If "remove from my account" wiped the phone as well, it would be
a remote-wipe button labelled as a tidy-up — the sort of action people discover the meaning of
afterwards.

Requiring a connection avoids a local flag flipped with no server delete behind it, which would leave
a copy on the server with nothing in the UI pointing at it — the worst outcome for a privacy-motivated
action.

## Acceptance criteria

```gherkin
Given a claimed group
When the user removes it from their account and confirms
Then no server copy of the group remains
And the group is still present and usable on this device
And it no longer syncs

Given the removal confirmation
When it is displayed
Then it states that the local copy stays
And it distinguishes removal from deleting the group

Given a group removed from the account on device A
When device B syncs
Then the group stops syncing on device B
And device B still holds its local copy

Given a claimed group with a completed draw
When it is removed from the account
Then the server-side assignments are deleted

Given a group removed from the account
When previously shared reveal links are opened
Then they decode and display exactly as before

Given a group that was removed from the account
When the user claims it again
Then it syncs again from the claiming device's copy

Given a group already removed from the account on another device
When the user removes it here
Then the local account scope is cleared and no error is surfaced

Given a device with no connection
When the user attempts to remove a group from their account
Then the action is unavailable and the reason is stated
```

## Dependencies & related

- Derived from: [UC-SS-008](../../use-cases/secret-santa/UC-SS-008-remove-group-from-account.md)
- Related: [FR-025](FR-025-claim-local-groups.md), [FR-023](FR-023-sync-across-devices.md),
  [FR-015](FR-015-delete-account.md) — the same principle at account scale: every server copy goes,
  every local copy survives,
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Governing ADRs: [ADR-0017](../../adr/0017-anonymous-to-account-claim.md),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) (user-requested removal is a hard
  delete)
- Implemented by: [FEAT-004](../../features/FEAT-004-m3-sync-and-claim.md) *(M3 — sync & claim; `Done`, built & verified 2026-08-12)*
- Glossary: [Claim](../../glossary/claim.md), [Tombstone](../../glossary/tombstone.md),
  [Group](../../glossary/group.md)
