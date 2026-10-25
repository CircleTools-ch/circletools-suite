---
id: FR-038
title: A group is kept on this device or online, chosen at creation and changeable
type: functional
status: Implemented
priority: Must
source: UC-SS-017
verification: test
related: [UC-SS-017, FR-039, FR-040, FR-041, FR-043, FR-048, NFR-001, ADR-0086, ADR-0015, FEAT-027]
---

# FR-038 — A group is kept on this device or online, chosen at creation and changeable

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

Every [Group](../../glossary/group.md) is kept in exactly one [place](../../glossary/place.md):

- **On this device** *(the default)* — the group lives only in the device's local storage
  ([ADR-0015](../../adr/0015-mvp-client-offline-storage.md)). Nothing reaches the server; people get their pairing
  through [reveal links](../../glossary/reveal-link.md).
- **Online** — the group is an [online group](../../glossary/online-group.md): one copy on the server
  ([FR-039](FR-039-shared-by-link-storage-and-sync.md)), organized by its [owner](../../glossary/owner.md) and reached by its
  [participants](../../glossary/participant.md) from their own devices. Signed in, the account owns it; signed out, the device
  keeps the [organizer link](../../glossary/organizer-link.md) ([FR-040](FR-040-admin-link-capability.md)).

Rules:

- **The place is chosen when the group is created**, with the user's default place pre-selected
  ([FR-048](FR-048-default-storage-mode-for-new-groups.md)).
- **It can be changed any time** in the group's *Sharing & sync*:
  - *on this device → online* uploads the group and makes this account (or a new organizer link) its owner;
  - *online → on this device* deletes the online copy — its links stop working and its participants lose
    access — and keeps the device's copy. Only the owner can do this.
- **Each change says what it does** in one plain line and happens only after confirmation.

## Rationale

A group grows from private to shared without a second model: the same group, kept in one more place. Two places
instead of four kinds (on this device, shared by link, my account, my account · shared) leave one way to be in a
group and one store to reason about (ADR-0086), and the default stays local for anyone who does nothing
(NFR-001).

## Acceptance criteria

```gherkin
Given a user creates a group without changing the place
When the group is created
Then it is kept on this device and nothing is sent to the server

Given the create-group field
When it is shown
Then both places are offered with a one-line explanation, and the default place is selected

Given a group on this device and a signed-in user
When the user puts it online and confirms
Then the group is uploaded, the account owns it, and its place is "online"

Given a group on this device and no signed-in user
When the user puts it online and confirms
Then the group is uploaded, an organizer link owns it, and this device keeps the link

Given an online group this device owns
When the owner keeps it only on this device and confirms
Then the online copy is deleted, its links stop working, and the device's copy remains
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md)
- Related: [FR-039](FR-039-shared-by-link-storage-and-sync.md), [FR-040](FR-040-admin-link-capability.md),
  [FR-043](FR-043-release-ownership-and-deletion-disposition.md), [FR-048](FR-048-default-storage-mode-for-new-groups.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary: [Place](../../glossary/place.md), [Online group](../../glossary/online-group.md), [Owner](../../glossary/owner.md)
