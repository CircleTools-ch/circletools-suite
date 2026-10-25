---
id: FR-027
title: Create a group from a circle, with its people taking part
type: functional
status: Implemented
priority: Must
source: UC-SS-009
verification: test
related: [UC-SS-009, FR-019, FR-022, FR-041, NFR-001, NFR-006, ADR-0086, ADR-0018, FEAT-005, FEAT-027]
---

# FR-027 — Create a group from a circle, with its people taking part

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

A signed-in user can create a group from one of their [circles](../../glossary/circle.md), choosing which of its
people to bring in:

- Every chosen person becomes a [Member](../../glossary/member.md). The circle person is recorded as the member's origin and
  never looked up again, so a deleted circle never breaks the group ([FR-022](FR-022-delete-circle.md)).
- The group goes **online at once**, owned by the signed-in account — it is for the people in the circle.
- Every chosen person **with an account** becomes a [participant](../../glossary/participant.md) of their member straight away;
  the server checks that the circle is shared with the organizer
  ([NFR-006](../non-functional/NFR-006-circle-authorization.md)). The organizer's own member is connected to their
  account. Managed members (no account) are plain members.
- If two chosen people resolve to the same account, the first takes part and the others are added without it; the
  user is told which.
- Offline, the group is created on this device and can be put online later.

The group's document never carries account ids: who takes part lives in the group store's
[role assignments](../../glossary/role-assignment.md).

## Rationale

A circle already says who belongs together and who has an account. Creating the group from it should leave those
people taking part, without inviting each of them again.

## Acceptance criteria

```gherkin
Given a signed-in user and a circle with two account holders and one managed member
When the user creates a group from the circle with all three
Then the group is online, owned by the user, and both account holders take part as their members

Given two chosen people resolve to the same account
When the group is created
Then only the first takes part and the user is told about the second

Given the device is offline
When the user creates a group from a circle
Then the group is kept on this device with all chosen members
```

## Dependencies & related

- Derived from: [UC-SS-009](../../use-cases/secret-santa/UC-SS-009-create-group-from-circle.md)
- Related: [FR-041](FR-041-join-link-and-person-claim.md), [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md); [ADR-0018](../../adr/0018-circle-and-sharing-model.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
