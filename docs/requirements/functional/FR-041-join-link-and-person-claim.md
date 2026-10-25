---
id: FR-041
title: A join link lets a person take part in an online group as one member
type: functional
status: Implemented
priority: Must
source: UC-SS-019
verification: test
related: [UC-SS-019, FR-028, FR-039, FR-040, FR-042, NFR-002, ADR-0086, FEAT-027]
---

# FR-041 — A join link lets a person take part in an online group as one member

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

The organizer can turn a [join link](../../glossary/join-link.md) on for an online group, turn it off, and replace it with a
fresh one. Opening it shows the group's name and asks:

- **Who you are** — one of the members whose spot is still free, or a new name (added to the roster).
- **How to remember you** — with your account (on all your devices) or **just this device**.

Continuing makes the person a [participant](../../glossary/participant.md) of that member and opens their participant page.
A member has at most one participant, and an account is at most one member per group: a member already taken is
shown as taken and cannot be picked.

The join link is one of four routes to a participant; the others are an email invitation
([FR-028](FR-028-link-member-to-account.md)), a circle ([FR-027](FR-027-create-group-from-circle.md)) and the
organizer's own member ([FR-032](FR-032-add-organizer-as-member.md)). All four create the same participant.

## Rationale

Everyone in the group should be able to see their own pairing and keep their own wishlist, without the organizer
typing it for them and without needing an account. One participant model behind every route keeps that the same
whichever way a person came in.

## Acceptance criteria

```gherkin
Given an online group with its join link on
When a person opens it
Then they see the group's name and the members whose spot is free

Given a person picks a free member and "just this device"
When they continue
Then this device holds a device key for that member and opens their participant page

Given a signed-in person picks a free member and "with my account"
When they continue
Then the group appears among the groups they take part in on all their devices

Given a member already has a participant
When another person opens the join link
Then that member is shown as taken and cannot be picked

Given the organizer turns the join link off
When the link is opened
Then it no longer lets anyone in
```

## Dependencies & related

- Derived from: [UC-SS-019](../../use-cases/secret-santa/UC-SS-019-join-a-shared-group.md)
- Related: [FR-028](FR-028-link-member-to-account.md), [FR-039](FR-039-shared-by-link-storage-and-sync.md),
  [FR-042](FR-042-release-a-claimed-person.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary: [Join link](../../glossary/join-link.md), [Participant](../../glossary/participant.md), [Device key](../../glossary/device-key.md)
