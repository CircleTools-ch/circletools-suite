---
id: UC-SS-008
title: Take a group back out of my account
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Should
related: [FR-026, NFR-002, NFR-008, ADR-0017]
---

# UC-SS-008 — Take a group back out of my account

## Summary / goal

An organizer who took a [Group](../../glossary/group.md) into their account changes their mind: the
server copy goes, the group stops syncing, and the copy on this device keeps working exactly as it did
before there was an account. Without this, [claiming](../../glossary/claim.md) would be a one-way
door — and a one-way door is not something people walk through willingly.

## Primary actor & stakeholders

- **Primary actor:** the organizer, on a device holding the group.
- **Stakeholders & interests:** the organizer, who wants a specific group off the server without
  deleting their account; the members, whose data (including any
  [Assignments](../../glossary/assignment.md)) is what leaves the server.

## Preconditions

- The actor is signed in and the group is account-scoped.
- The device is online — removing the server copy is a server operation.

## Postconditions

- **Success:** no server copy of the group exists; the group no longer syncs; the local copy on this
  device is intact and fully usable.
- **Failure:** the group is unchanged and still syncing.

## Trigger

The actor chooses **Remove from my account** on the group.

## Main success scenario

1. Actor chooses **Remove from my account**.
2. System states what happens: the copy on the server is deleted, including the
   [Draw](../../glossary/draw.md) if one has run; the group **stays on this device** and keeps
   working; it stops appearing on the actor's other devices; already-shared
   [reveal links](../../glossary/reveal-link.md) are unaffected.
3. Actor confirms.
4. System deletes the server copy and clears the group's account scope locally. The group is now an
   ordinary account-free group on this device.

## Extensions / alternate flows

- **2a. The actor meant to delete the group entirely** → deleting is a different action with a
  different outcome, and the confirmation says so plainly. Deleting removes it everywhere via a
  [tombstone](../../glossary/tombstone.md); removing from the account removes it from the server only.
  Confusing the two costs someone their data.
- **4a. The actor's other devices sync afterwards** → the group stops syncing there and **their local
  copies stay**. "Take it out of my account" is not a remote wipe; a device that had the group keeps
  what it had, frozen at the last synced version.
- **4b. The actor claims it again later** → allowed, and it re-uploads from whichever device performs
  the claim. Each device's copy may by then differ; the claiming device's copy is what lands.

## Exceptions

- **The device is offline** → unavailable, and the reason is stated. A local flag flipped without the
  server delete would leave the copy on the server with nothing pointing at it.
- **The group was already removed from the account on another device** → the local scope is cleared
  and no error is surfaced; the end state is what was asked for.
- **The account is deleted instead** ([UC-MAIN-006](../main/UC-MAIN-006-delete-account-and-export-data.md))
  → every server copy goes at once, and every local copy on every device survives. Same principle,
  applied to all groups.

## Non-functional notes

- **Offline/local-first:** the outcome *is* the account-free product — the group returns to being
  exactly what [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md) describes.
- **Localization:** DE/EN/FR, and the wording carries the whole weight of the distinction in
  extension 2a — this is a confirmation that must be read, not skimmed.
- **Privacy:** the server-side copy of the members, wishlists and assignments is deleted, not
  soft-deleted, per [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)'s stance on
  user-requested removal.
- **Performance:** a single round trip.

## Open questions

- **Does the tombstone that stops other devices syncing also need retaining?** It must live long
  enough for every device to see it, which is the same retention question deletes have
  ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md) leaves it to the M3 feature spec) — but
  with the opposite consequence if it is pruned too early: the group would simply keep syncing.

## Traceability

- Requirement: [FR-026](../../requirements/functional/FR-026-remove-group-from-account.md)
- ADRs: [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) ("a claimed group can be removed from
  the account, which leaves the local copy intact"),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- Related use cases: [UC-SS-007](UC-SS-007-claim-local-groups.md),
  [UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md),
  [UC-MAIN-006](../main/UC-MAIN-006-delete-account-and-export-data.md)
- Glossary: [Claim](../../glossary/claim.md), [Tombstone](../../glossary/tombstone.md),
  [Sync](../../glossary/sync.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D4
