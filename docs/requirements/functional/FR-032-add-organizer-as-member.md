---
id: FR-032
title: The signed-in organizer is a member of the group they create
type: functional
status: Implemented
priority: Should
source: UC-SS-001
verification: test
related: [UC-SS-001, FR-001, FR-029, FR-030, FR-041, NFR-001, ADR-0086, FEAT-027]
---

# FR-032 — The signed-in organizer is a member of the group they create

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

When a **signed-in** user creates a group, they are added as its first [Member](../../glossary/member.md) under their display
name. If the group is created online, that member is connected to their account at once (the *own member* route to
a [participant](../../glossary/participant.md)), so they see their own assignment ([FR-029](FR-029-see-my-assignment.md)).

On the members page the organizer can also mark any member as themselves (*This is me*) in an online group, for a
group created before they signed in or put online later.

Signed out, a new group starts empty ([NFR-001](../non-functional/NFR-001-local-first-offline.md)). Adding or
connecting the organizer never blocks creating the group; offline, the connection is made when the group goes
online.

## Rationale

Organizers almost always take part themselves; the app already knows who they are when they are signed in.

## Acceptance criteria

```gherkin
Given a signed-in user named Ada
When she creates a group online
Then the group's first member is Ada, shown as connected

Given an online group and a signed-in organizer not yet in the roster as themselves
When they choose "This is me" on a member
Then that member is connected to their account

Given a signed-out user
When they create a group
Then the group has no members
```

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md)
- Related: [FR-001](FR-001-create-secret-santa-group.md), [FR-029](FR-029-see-my-assignment.md),
  [FR-041](FR-041-join-link-and-person-claim.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
