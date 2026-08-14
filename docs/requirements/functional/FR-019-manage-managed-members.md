---
id: FR-019
title: Add and maintain managed members
type: functional
status: Approved
priority: Must
source: UC-MAIN-011
verification: test
related: [UC-MAIN-011, UC-MAIN-009, UC-MAIN-010, NFR-003, NFR-004, NFR-006, ADR-0018, ADR-0022, ADR-0043, FEAT-003]
---

# FR-019 — Add and maintain managed members

## Description

A circle **owner** or **admin** must be able to add a
[Managed member](../../glossary/managed-member.md) — a person in the
[Circle](../../glossary/circle.md) with a profile but **no account** — and to edit and remove them.

- A managed member has a **display name** (required) and an optional **preferred language**. Nothing
  else. There is no email address, by definition: an address is what makes someone an
  [Invitation](../../glossary/invitation.md) instead.
- They hold a [membership](../../glossary/circle-membership.md) in the `member` role and **may never
  hold `admin` or `owner`** — those roles imply someone who can sign in.
- They are visibly marked as **managed** in the people list.
- Display names are **not unique**; two people with the same name are allowed.
- **Promotion:** when the person does get an email address, they are promoted through an
  [Invitation](../../glossary/invitation.md) aimed at the managed profile. The profile is aimed one of
  two ways: the inviting owner or admin picks it when sending, **or** — when they don't — the invited
  person picks themselves out of the circle's offline people as they accept, a choice offered only to
  the signed-in recipient whose verified address is the invited one
  ([ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)). Either way, on acceptance
  the existing membership is **linked to the new [User](../../glossary/user.md)** rather than replaced,
  so the person keeps their place and their history. A recipient's pick that no longer names a managed
  member of the circle is ignored — they simply join as new. Every rule of
  [FR-018](FR-018-respond-to-circle-invitation.md) applies unchanged.
- Managed members are **circle data, not the creator's data**: they survive the departure or account
  deletion of the admin who created them, and are deleted with the circle
  ([FR-022](FR-022-delete-circle.md)).

## Rationale

A family Secret-Santa without the children is pointless, and [ADR-0018](../../adr/0018-circle-and-sharing-model.md)
(decision D7) put managed members in the model from the start precisely so that invitations, sync and
linking never bake in the assumption "member = account" — an assumption that is very expensive to
unpick later.

The deliberately thin profile is a data-minimisation decision with a specific reason: this is personal
data about someone — often a child — who did not enter it, cannot review it, and cannot ask for it to
be corrected. A birth date or a photo would each need a justification that the product does not have
([NFR-004](../non-functional/NFR-004-data-protection-and-retention.md)).

Promotion by *linking* rather than by creating a second person is what keeps the promise that a
managed member is a real participant rather than a placeholder to be replaced later.

## Acceptance criteria

```gherkin
Given a circle owner or admin
When they add a managed member with a display name
Then the person appears in the circle's people list marked as managed
And they hold a membership in the member role

Given a managed member
When the people list is shown
Then no email address is stored or displayed for them
And they are visibly distinguished from account-backed people

Given a circle that already contains a person named "Anna"
When another person named "Anna" is added as a managed member
Then both exist and the addition is not rejected

Given a managed member
When an owner or admin edits their display name or preferred language
Then the change is applied
And no sign-in is possible for that person at any point

Given a managed member
When an attempt is made to give them the admin or owner role
Then it is refused

Given a user holding only the member role in a circle
When they call the managed-member endpoints directly
Then they are rejected server-side

Given a managed member who has since obtained an email address
When an admin invites them from the managed profile and the invitation is accepted
Then the existing membership is linked to the new user
And no second membership or duplicate person is created

Given a circle with offline people and an invitation not aimed at any of them
When the signed-in invited recipient picks one of those offline people while accepting
Then the existing membership is linked to their account
And no second membership or duplicate person is created

Given a holder of a forwarded invitation link who is not the invited address
When they request the circle's offline people to join as
Then the request is refused exactly as an unknown invitation is

Given a managed member created by an admin
When that admin leaves the circle or deletes their account
Then the managed member remains in the circle
And remaining owners and admins can maintain them

Given a managed member who was copied into a Secret-Santa group
When they are removed from the circle
Then the group's copy of that person is unchanged
And any completed draw is unchanged

Given a managed member is added
When the addition fails partway
Then neither a profile nor a membership remains
```

## Dependencies & related

- Derived from: [UC-MAIN-011](../../use-cases/main/UC-MAIN-011-manage-managed-members.md)
- Related: [FR-017](FR-017-invite-to-circle.md), [FR-020](FR-020-manage-circle-roles-and-membership.md),
  [FR-022](FR-022-delete-circle.md), [FR-015](FR-015-delete-account.md) (whose "what happens to
  managed members" question this requirement answers),
  [NFR-004](../non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D7),
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- **Decided in [FEAT-003](../../features/FEAT-003-m2-circles.md):** managed members **do** appear in
  another member's export — display name and the managed marker, exactly what the people list shows
  and nothing more, so ADR-0022's "mirrors in-app visibility" and NFR-006's no-addresses rule stay
  true together. The cap is **100 memberships per circle**, managed members included. On promotion the
  **account's display name wins**: one person, one name.
- Implemented by: [FEAT-003](../../features/FEAT-003-m2-circles.md) ·
  verified by [TP-003](../../features/TP-003-m2-circles.md)
- Glossary: [Managed member](../../glossary/managed-member.md),
  [Circle membership](../../glossary/circle-membership.md), [Member](../../glossary/member.md)
