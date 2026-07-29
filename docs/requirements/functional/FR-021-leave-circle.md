---
id: FR-021
title: Leave a circle
type: functional
status: Approved
priority: Must
source: UC-MAIN-013
verification: test
related: [UC-MAIN-013, UC-MAIN-012, UC-MAIN-014, NFR-001, NFR-006, ADR-0018]
---

# FR-021 — Leave a circle

## Description

A [User](../../glossary/user.md) must be able to leave a [Circle](../../glossary/circle.md) they
belong to, on their own, without an admin's permission.

Before confirming, they are told what leaving does and does not do: the
[membership](../../glossary/circle-membership.md) ends and access ends immediately; their **account
stays**; their own Secret-Santa groups, [Members](../../glossary/member.md) and completed
[Draws](../../glossary/draw.md) are untouched; rejoining requires a fresh
[Invitation](../../glossary/invitation.md).

The **owner** is the exception, and the way out depends on who else is there:

- other account-backed people remain → leaving is refused until ownership is transferred
  ([FR-020](FR-020-manage-circle-roles-and-membership.md)), and the refusal offers the transfer
  directly;
- nobody else can own it — the circle is empty, or holds only
  [managed members](../../glossary/managed-member.md), who cannot sign in → leaving is offered as
  **deleting the circle** ([FR-022](FR-022-delete-circle.md)), naming what will be deleted.

A managed member cannot leave; an owner or admin removes them
([FR-019](FR-019-manage-managed-members.md)).

## Rationale

Leaving must not need permission — a circle is a social relationship, and requiring an admin to
release someone from a group they no longer want to be in (a dissolved friend group, a family after a
separation) makes the product an instrument in the argument.

Refusing to let the sole owner simply leave is what keeps the model's invariant true: exactly one
owner, always. Because the owner cannot leave an occupied circle, and cannot be removed, **a circle is
never memberless** — which is why nothing in the system has to define what an ownerless circle means.
The refusal must offer the transfer inline; a dead end here is what makes people delete things they
meant to keep.

## Acceptance criteria

```gherkin
Given a member or admin of a circle
When they choose to leave and confirm
Then their membership is deleted
And the circle no longer appears in their list

Given a user about to leave a circle
When the confirmation is shown
Then it states that their account remains
And that their own Secret-Santa groups, members and completed draws are untouched
And that rejoining requires a new invitation

Given a user who has left a circle
When they request that circle's data with a still-valid access token
Then the request is refused

Given the owner of a circle with other account-backed people in it
When they attempt to leave
Then it is refused
And transferring ownership is offered in the same place

Given the owner of a circle with no other account-backed people
When they attempt to leave
Then deleting the circle is offered as the way out
And the confirmation names the managed members that would be deleted with it

Given a user who left a circle
When they are invited again later
Then they join as a member
And their previous role is not restored

Given a user whose membership was already removed by an admin
When they attempt to leave
Then the circle is simply absent from their list and no error is surfaced

Given a managed member
When any leave action is attempted for them
Then it is refused, because they cannot sign in
```

## Dependencies & related

- Derived from: [UC-MAIN-013](../../use-cases/main/UC-MAIN-013-leave-circle.md)
- Related: [FR-020](FR-020-manage-circle-roles-and-membership.md),
  [FR-022](FR-022-delete-circle.md), [FR-015](FR-015-delete-account.md) (the same sole-owner guard,
  reached from account deletion),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) ("membership is what a person
  leaves"; the sole-owner edge case it deferred here)
- Glossary: [Circle membership](../../glossary/circle-membership.md), [Circle](../../glossary/circle.md)
