---
id: FR-022
title: Delete a circle
type: functional
status: Approved
priority: Must
source: UC-MAIN-014
verification: test
related: [UC-MAIN-014, UC-MAIN-006, FR-015, NFR-004, NFR-006, ADR-0018, ADR-0022, FEAT-003]
---

# FR-022 — Delete a circle

## Description

The **owner** of a [Circle](../../glossary/circle.md) must be able to delete it. Deletion is
**immediate and irreversible**, with no grace period and no soft-delete state, and it runs as **one
transaction**.

Before it proceeds, the owner is shown precisely what happens:

| Deleted | Not deleted |
|---|---|
| the circle, its name and label | every user account, including the other members' |
| every [membership](../../glossary/circle-membership.md) | Secret-Santa groups created from this circle, and their [Members](../../glossary/member.md) |
| every [managed member](../../glossary/managed-member.md) profile | completed [Draws](../../glossary/draw.md) and already-issued [reveal links](../../glossary/reveal-link.md) |
| every pending [Invitation](../../glossary/invitation.md) | anything stored on anyone's device |

The confirmation **names the circle**, states how many people will lose it, and **names the managed
members** — whose profiles exist nowhere else. Other members are **not** asked; ownership is the right
to do this.

A Secret-Santa group that referenced the circle keeps working: it holds at most a circle identifier
which now resolves to nothing, and the Secret-Santa side must **tolerate that rather than treat it as
corruption**.

This requirement unblocks the sole-circle-owner guard that
[FR-015](FR-015-delete-account.md) has been carrying since M1: an owner deleting their account either
transfers the circle or deletes it here.

## Rationale

Immediate hard deletion matches account deletion ([ADR-0022](../../adr/0022-account-deletion-and-data-export.md))
and for the same reason: a lifecycle state that every query, membership check and invitation lookup
must then reason about is a permanent tax paid for a rare mistake. A circle is also the cheapest thing
in the suite to recreate — a name, a label and some invitations.

Naming the managed members in the confirmation is the one place where this differs from deleting your
own data. Everything else in the circle exists elsewhere too; those profiles do not, and the person
they describe is not in the room.

Not asking the other members is deliberate. A vote would give an inactive or departed member a veto
over the owner's own data, and the model already concentrates exactly these two rights — delete and
transfer — in the owner.

## Acceptance criteria

```gherkin
Given the owner of a circle
When they confirm deletion
Then the circle, all memberships, all managed-member profiles and all pending invitations are deleted
And no user account is deleted

Given an owner about to confirm deletion
When the confirmation is shown
Then it names the circle
And it states how many people will lose access
And it names every managed member that will be deleted
And it states that Secret-Santa groups, draws and reveal links are unaffected

Given an admin of a circle
When they attempt to delete it
Then it is refused server-side

Given a deleted circle
When a Secret-Santa group that was created from it is opened
Then the group, its members and its completed draw are unchanged
And its dangling circle identifier does not cause an error

Given a deleted circle
When another member opens the app
Then they are told the circle no longer exists
And they are not shown an error screen

Given a pending invitation to a circle
When the circle is deleted
Then the invitation can no longer be accepted
And the recipient is told the circle no longer exists

Given a circle deletion that fails partway
When the failure occurs
Then nothing is deleted
And the owner is not told it succeeded

Given a user who is the sole owner of a circle and is deleting their account
When they delete the circle here
Then account deletion may proceed
```

## Dependencies & related

- Derived from: [UC-MAIN-014](../../use-cases/main/UC-MAIN-014-delete-circle.md)
- Related: [FR-015](FR-015-delete-account.md) (its sole-circle-owner guard is unblocked by this),
  [FR-021](FR-021-leave-circle.md), [FR-019](FR-019-manage-managed-members.md),
  [NFR-004](../non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md),
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- **Decided in [FEAT-003](../../features/FEAT-003-m2-circles.md):** **no password** — the owner types
  the circle's **name** back. Account deletion asks for a password because the threat there is a
  stolen session taking everything; here the threat is the misclick, and a hijacker who wants to hurt
  you deletes the account, which is already guarded. Typing the name defends against the real failure
  mode without training users to type their password into in-app prompts. **No pre-deletion export
  window:** every member can already export at any time, and a window is a soft-delete state by
  another name.
- Implemented by: [FEAT-003](../../features/FEAT-003-m2-circles.md) ·
  verified by [TP-003](../../features/TP-003-m2-circles.md)
- Glossary: [Circle](../../glossary/circle.md), [Managed member](../../glossary/managed-member.md),
  [Invitation](../../glossary/invitation.md)
