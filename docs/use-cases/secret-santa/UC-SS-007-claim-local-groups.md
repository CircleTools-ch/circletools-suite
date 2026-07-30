---
id: UC-SS-007
title: Take my existing groups into my account
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Must
related: [FR-025, NFR-001, NFR-002, NFR-008, ADR-0017, ADR-0020]
---

# UC-SS-007 — Take my existing groups into my account

## Summary / goal

Someone who has been using the app without an account signs in for the first time and decides, **per
[Group](../../glossary/group.md)**, which of the groups already on this device should be taken into
the account and synced. This runs exactly once per device, at the moment the user is deciding whether
the account was a good idea — and it is the moment their data first leaves the device.

## Primary actor & stakeholders

- **Primary actor:** an organizer who created groups before having an account.
- **Stakeholders & interests:** the organizer, for whom the local copy is the **only** copy; the
  members of those groups, whose names, [Wishlists](../../glossary/wishlist.md) and — if a
  [Draw](../../glossary/draw.md) has run — [Assignments](../../glossary/assignment.md) are what gets
  uploaded; anyone sharing the device, whose groups are indistinguishable from the actor's own.

## Preconditions

- The device holds at least one group created without an account.
- The actor has just signed in on this device for the first time
  ([UC-MAIN-002](../main/UC-MAIN-002-sign-in-with-password.md) /
  [UC-MAIN-004](../main/UC-MAIN-004-sign-in-with-passkey.md)).

## Postconditions

- **Success:** the selected groups exist in the account and sync
  ([UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md)); the unselected ones are unchanged and
  still local; **every** group is still on the device.
- **Failure:** nothing was uploaded, and every group is still on the device exactly as before.

## Trigger

First successful sign-in on a device that holds account-free groups.

## Main success scenario

1. System detects local groups with no account behind them and shows them as a list — name, number of
   members, whether the draw has run.
2. System states plainly what claiming does: the group is copied to the account and syncs to the
   actor's other devices; **the local copy stays on this device either way**; groups left behind keep
   working exactly as they do now, **on this device only** — and if the browser is cleared, they are
   gone.
3. Where a group's draw has run, the same screen says that its assignments are uploaded too, and that
   the operator can technically read them
   ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).
4. Actor selects the groups to take. **Nothing is selected by default.**
5. Actor confirms. System uploads the selected groups, marks them account-scoped, and keeps the local
   copy as a cache.
6. The unselected groups are listed as before, with no nagging and no second-class marking.

## Extensions / alternate flows

- **1a. There are no local groups** → no screen; sign-in completes normally. This must not become a
  screen everyone dismisses.
- **4a. Actor selects nothing and continues** → nothing is uploaded, and the prompt does not return.
  Claiming stays available per group from the group's own screen — the one-time prompt is a
  convenience, not the only door.
- **5a. A group is claimed later from its own screen** → identical outcome; this is the same
  operation, not a lesser one.
- **5b. The upload fails partway** → the groups that arrived are account-scoped; the rest are still
  purely local and can be claimed again. Because ids are client-minted
  ([ADR-0008](../../adr/0008-entity-id-strategy.md)), re-claiming a group that did arrive updates it
  rather than duplicating it.
- **5c. The actor claims on a second device a group that is "the same" group they already claimed
  elsewhere** → it becomes a **duplicate**, and is left as one. The two local copies have unrelated
  ids and no shared origin; heuristic matching on names and members would sometimes merge two
  genuinely different groups, which is worse than a visible duplicate the user can delete
  ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)).
- **6a. The actor changes their mind** → the group can be removed from the account again
  ([UC-SS-008](UC-SS-008-remove-group-from-account.md)), which leaves the local copy intact.

## Exceptions

- **Sign-in is on a shared device and the groups belong to someone else** → nothing distinguishes
  them: anonymous data carries no identity, so there is nothing to check against. Accepted and
  inherent to account-free storage; the wording therefore names each group rather than saying "your
  groups".
- **The device is offline at first sign-in** → sign-in itself needs the network, so this does not
  arise; if connectivity drops during the upload, see 5b.
- **Local storage refuses a write while marking the claim** → the claim is not recorded and is
  reported ([ADR-0029](../../adr/0029-storage-write-failure-contract.md)); the group stays local.

## Non-functional notes

- **Offline/local-first:** unclaimed groups are exactly the account-free product
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)). Claiming adds a
  server copy; it never removes the local one, so the action is additive in the literal sense.
- **Localization:** DE/EN/FR, including the counted strings on this screen — *"3 groups on this
  device"* has a singular, in all three languages
  ([localization guidelines](../../guidelines/localization-guidelines.md)).
- **Privacy:** this screen is where the amended
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md) becomes visible to a
  user. It must say what is uploaded — members, wishlists and, for a completed draw, the assignments
  — before it happens, not in a settings page afterwards
  ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)).
- **Performance:** a family-scale claim is a handful of small aggregates; the screen must not block on
  the upload without showing progress.

## Open questions

- **Should a completed draw be claimable at all in M3?** Recommendation: yes. Excluding it would mean
  the account cannot hold the group people actually care about, and M4's in-app reveal has to build on
  a synced draw anyway.
- **Do reveal links already in circulation keep working after a claim?** They must, and they do — a
  [reveal link](../../glossary/reveal-link.md) is self-contained and decodes client-side
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)). Worth an explicit test rather than
  an assumption.

## Traceability

- Requirement: [FR-025](../../requirements/functional/FR-025-claim-local-groups.md); constrained by
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)
- ADRs: [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (choose per group; local kept as
  cache), [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md),
  [ADR-0008](../../adr/0008-entity-id-strategy.md)
- Related use cases: [UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md),
  [UC-SS-008](UC-SS-008-remove-group-from-account.md),
  [UC-SS-001](UC-SS-001-create-group.md)
- Glossary: [Claim](../../glossary/claim.md), [Sync](../../glossary/sync.md),
  [Group](../../glossary/group.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D4
