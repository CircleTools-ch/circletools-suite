---
id: FR-020
title: Manage circle roles, membership and ownership
type: functional
status: Implemented
priority: Must
source: UC-MAIN-012
verification: test
related: [UC-MAIN-012, UC-MAIN-013, UC-MAIN-014, NFR-005, NFR-006, ADR-0018, FEAT-003]
---

# FR-020 — Manage circle roles, membership and ownership

## Description

A [Circle](../../glossary/circle.md) must be administrable, with rights split exactly as
[ADR-0018](../../adr/0018-circle-and-sharing-model.md) sets them out:

| Action | Owner | Admin | Member |
|---|:---:|:---:|:---:|
| Edit the circle's name and label | ✅ | ✅ | — |
| Invite people; revoke and resend invitations | ✅ | ✅ | — |
| Add, edit and remove [managed members](../../glossary/managed-member.md) | ✅ | ✅ | — |
| Remove a **member** | ✅ | ✅ | — |
| Remove an **admin** | ✅ | — | — |
| Change someone's role (member ⇄ admin) | ✅ | — | — |
| Transfer ownership | ✅ | — | — |
| Delete the circle ([FR-022](FR-022-delete-circle.md)) | ✅ | — | — |
| See the circle and its people; leave ([FR-021](FR-021-leave-circle.md)) | ✅ | ✅ | ✅ |

Further rules:

- A circle has **exactly one owner** at all times. The owner cannot be removed or demoted by anyone,
  including themselves.
- **Transfer of ownership is immediate**, not an offer awaiting acceptance, and the previous owner
  becomes an **admin** in the same operation. Only an account-backed member or admin may receive it.
- Removing a person deletes their **membership only**: their account, their Secret-Santa groups, their
  [Members](../../glossary/member.md) and completed [Draws](../../glossary/draw.md) are untouched.
- Role and membership changes take effect on the affected person's **next request** — access is
  evaluated per request, never cached into a token
  ([NFR-006](../non-functional/NFR-006-circle-authorization.md)).
- The people list shows **display names, not email addresses**.

## Rationale

Role changes are owner-only because an admin who can appoint admins can manufacture a majority and
make the owner's exclusive rights — delete, transfer — negotiable. For the same reason an admin may
not remove another admin.

Transfer is immediate because the alternative, a pending offer, creates precisely the state the model
forbids: a circle whose ownership is in limbo. Nothing is lost by making it immediate — the new owner
can transfer it back or leave, and being made owner grants no view of anything they could not already
see.

Withholding email addresses from the people list is not incidental. Circle membership says nothing
about entitlement to another member's identity data, and a list of addresses would quietly rebuild the
enumeration surface [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md) closed —
join a circle, read the addresses, learn who has an account.

## Acceptance criteria

```gherkin
Given a circle owner
When they change a member's role to admin
Then the person holds the admin role
And their next request is authorized under it without signing out

Given a circle admin
When they attempt to change anyone's role
Then it is refused server-side

Given a circle admin
When they attempt to remove another admin
Then it is refused server-side

Given a circle admin
When they remove a member or a managed member
Then the membership is deleted
And the removed person's account and Secret-Santa data are unchanged

Given a removed member holding a valid access token
When they request the circle's data after removal
Then the request is refused

Given a circle owner
When they attempt to remove or demote themselves
Then it is refused, and transfer, leaving or deletion are offered instead

Given a circle owner transferring ownership to an account-backed member
When they confirm
Then that person is the owner and the previous owner is an admin
And the circle has exactly one owner

Given a circle owner
When they attempt to transfer ownership to a managed member
Then it is refused

Given a circle owner transferring ownership
When the target's membership has ended concurrently
Then the transfer fails and the circle keeps its current owner

Given any member of a circle
When they view the people list
Then display names are shown
And no email addresses are returned by the API

Given an owner or admin
When they change the circle's name or label
Then the change applies for every member
And the label is not translated for viewers in other languages
```

## Dependencies & related

- Derived from: [UC-MAIN-012](../../use-cases/main/UC-MAIN-012-manage-circle-people.md)
- Related: [FR-016](FR-016-create-circle.md), [FR-017](FR-017-invite-to-circle.md),
  [FR-019](FR-019-manage-managed-members.md), [FR-021](FR-021-leave-circle.md),
  [FR-022](FR-022-delete-circle.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (the role table; the transfer
  flow it deferred to this milestone)
- **Decided in [FEAT-003](../../features/FEAT-003-m2-circles.md):** a removed person gets **no mail** —
  the circle is simply gone from their list, and an open page says so on the next request; removal is
  a social act with no reply channel, and mailing it makes the product a participant in the argument.
  Elevated roles are surfaced as a badge on **owner and admin only**, so the owner sees at a glance
  who else can act without the list turning into badges.
- Implemented by: [FEAT-003](../../features/FEAT-003-m2-circles.md) ·
  verified by [TP-003](../../features/TP-003-m2-circles.md)
- Glossary: [Circle membership](../../glossary/circle-membership.md), [Circle](../../glossary/circle.md),
  [Managed member](../../glossary/managed-member.md)
