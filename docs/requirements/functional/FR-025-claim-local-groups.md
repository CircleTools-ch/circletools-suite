---
id: FR-025
title: Claim local groups into the account
type: functional
status: Approved
priority: Must
source: UC-SS-007
verification: test
related: [UC-SS-007, UC-SS-008, UC-MAIN-015, FR-023, FR-026, NFR-001, NFR-002, NFR-003, ADR-0017, ADR-0020, FEAT-004]
---

# FR-025 — Claim local groups into the account

## Description

On the **first sign-in on a device that holds account-free
[Groups](../../glossary/group.md)**, the user must be shown those groups and choose, **per group**,
which to [claim](../../glossary/claim.md) into the account.

- The list names each group and shows enough to identify it — members, whether the draw has run.
- **Nothing is selected by default.** Claiming is an opt-in action, not a pre-ticked box to notice.
- Before confirming, the screen states: the group is copied to the account and syncs to the user's
  other devices; **the local copy stays on this device**; groups left behind keep working, **on this
  device only**, and are lost if the browser storage is cleared.
- For any group whose draw has run, the screen also states that its
  [assignments](../../glossary/assignment.md) are uploaded and that the operator can technically read
  them ([NFR-002](../non-functional/NFR-002-assignment-privacy.md)).
- Claiming **never clears local data**, and unclaimed groups are neither marked as lesser nor nagged
  about.
- The prompt appears **once per device**. Claiming stays available afterwards **per group, from the
  group's own screen**, with an identical outcome.
- A partly failed upload leaves the arrived groups claimed and the rest purely local; re-claiming a
  group that did arrive updates it rather than duplicating it, because ids are client-minted
  ([ADR-0008](../../adr/0008-entity-id-strategy.md)).
- Claiming "the same" group from a second device produces a **duplicate**, and it is left as one.

If there are no local groups, no screen is shown.

## Rationale

This flow runs once per user, at the moment they are deciding whether the account was a good idea, and
it moves the only copy of real data they have. A silent merge or a silent replace is the fastest way
to make a sync feature untrustworthy on day one ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)).

Keeping the local copy after claiming is what makes the action honestly additive: a mistaken claim, or
a later account deletion, costs nothing.

No automatic de-duplication: two local copies of "the same" group on two devices have unrelated ids
and no shared origin. Heuristic matching on names and members would sometimes merge two genuinely
different groups — a family that runs one exchange with the in-laws and one with the neighbours — which
is worse than a visible duplicate the user can delete.

The warning that unclaimed groups live only on this device carries real weight: someone who claims
nothing and later wipes their browser loses everything, and may believe the account protected them.

## Acceptance criteria

```gherkin
Given a device holding three account-free groups
When the user signs in for the first time
Then all three are listed by name
And none of them is selected

Given the claim screen
When it is displayed
Then it states that the local copy stays on this device
And that unclaimed groups remain on this device only

Given a local group whose draw has run
When it appears on the claim screen
Then the screen states that its assignments will be uploaded

Given the claim screen with two of three groups selected
When the user confirms
Then those two exist in the account and sync
And the third is unchanged and still local
And all three are still on the device

Given a user who selects nothing and continues
When sign-in completes
Then nothing was uploaded
And the prompt does not appear again on this device

Given an unclaimed group
When the user opens it later
Then it offers to be claimed
And claiming it there has the same outcome as the first-sign-in prompt

Given an upload that fails after the first of two groups
When the user retries
Then the group that arrived is updated rather than duplicated

Given a device with no account-free groups
When the user signs in for the first time
Then no claim screen is shown

Given a claimed group
When the user's already-shared reveal links are opened
Then they decode and display exactly as before

Given the claim screen in German, English and French
When one group is listed
Then the count reads as a singular in each language
```

## Dependencies & related

- Derived from: [UC-SS-007](../../use-cases/secret-santa/UC-SS-007-claim-local-groups.md)
- Related: [FR-023](FR-023-sync-across-devices.md),
  [FR-026](FR-026-remove-group-from-account.md) (the way back out),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) — the claim screen is where its amendment
  becomes visible to a user,
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADRs: [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (choose per group; local
  kept as cache; duplicates accepted), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md),
  [ADR-0008](../../adr/0008-entity-id-strategy.md),
  [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (links stay self-contained)
- Implemented by: FEAT-004 *(M3 — sync; not yet written)*
- Glossary: [Claim](../../glossary/claim.md), [Group](../../glossary/group.md),
  [Sync](../../glossary/sync.md)
