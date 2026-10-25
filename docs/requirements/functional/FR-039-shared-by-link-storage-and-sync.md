---
id: FR-039
title: An online group is kept once on the server, and every role reads its own view of it
type: functional
status: Implemented
priority: Must
source: UC-SS-017
verification: test
related: [UC-SS-017, UC-SS-019, FR-038, FR-040, FR-041, NFR-002, NFR-009, NFR-010, ADR-0086, ADR-0016, FEAT-027]
---

# FR-039 — An online group is kept once on the server, and every role reads its own view of it

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

An [online group](../../glossary/online-group.md) has **one copy** on the server, in the Secret-Santa group store. Who may do
what is a list of [role assignments](../../glossary/role-assignment.md): one [owner](../../glossary/owner.md) — an account or the
[organizer link](../../glossary/organizer-link.md) — and any number of [participants](../../glossary/participant.md), each held by an
account or a [device key](../../glossary/device-key.md).

- **Organizers** read and write the whole document. A push carries the version it was based on; the rule is last
  write wins per group ([ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)). A device pushes its unsent
  edits before it reads.
- **Participants** read only their own view: per event, whom they give to and that person's wishlist and gift
  note — never the whole draw ([NFR-002](../non-functional/NFR-002-assignment-privacy.md)).
- **What members keep themselves** — the [member entry](../../glossary/member-entry.md) — is stored apart and merged into the
  document on every write, so the organizer's copy, the participant's view and every reveal page show the same
  text, and an organizer's older copy never overwrites it.
- **Reveal pages** are computed from the current document when they are read, so they always show the latest
  wishlist, budget and date.
- A request without a role in the group gets the same "not found" as a group that does not exist.

## Rationale

One copy of each kind of information, in one place, read through one access model, replaces three stores that
had to be kept in step (the shared-group store, the account sync and the linking projection). Deriving every view
on read means nothing published can go stale.

## Acceptance criteria

```gherkin
Given an online group and its owner
When the owner's device reads the group
Then it receives the whole document, its version and who has which role

Given an organizer device with an unsent edit
When it syncs
Then the edit is pushed before the group is read back

Given a participant
When their device reads the group
Then it receives only their own view: their pairing per event and that recipient's wishlist and gift note

Given a participant writes their wishlist
When the owner reads the group next
Then the document holds the participant's wishlist, and a push from an older copy does not overwrite it

Given a caller without a role in the group
When it reads the group
Then the answer is "not found", the same as for a group that does not exist
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md),
  [UC-SS-019](../../use-cases/secret-santa/UC-SS-019-join-a-shared-group.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md), [FR-040](FR-040-admin-link-capability.md),
  [FR-041](FR-041-join-link-and-person-claim.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md),
  [NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md),
  [NFR-010](../non-functional/NFR-010-shared-group-retention-lifecycle.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
