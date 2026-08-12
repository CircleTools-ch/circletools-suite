---
id: FR-028
title: Link a member to an account
type: functional
status: Draft
priority: Must
source: UC-SS-010
verification: test
related: [UC-SS-010, UC-SS-009, UC-SS-011, UC-SS-012, FR-018, FR-015, NFR-002, NFR-005, ADR-0019, ADR-0020, ADR-0018, FEAT-005]
---

# FR-028 — Link a member to an account

## Description

An organizer must be able to link an individual [Member](../../glossary/member.md) of a
[Group](../../glossary/group.md) to a suite [User](../../glossary/user.md), and to unlink one.

- **Establishing a link requires the account holder's consent.** The organizer enters the person's
  email address; the system sends a **link request** and reports only *"request sent"*, **never**
  whether the address has an account
  ([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)). The member shows as
  **link pending** until the request is accepted, declined, or expires. The link is established
  **only on acceptance**.
- A **self-link** — the signed-in organizer linking their **own** account to a member — is
  auto-accepted; no second confirmation is needed.
- On linking, the member gains an **optional user reference**
  ([ADR-0019](../../adr/0019-member-user-linking.md)); wishlist ownership moves to that user
  ([FR-030](FR-030-edit-my-wishlist.md)); and, once the draw has run, that user may read **only their
  own** assignment ([FR-029](FR-029-see-my-assignment.md)). **The link confers no organizer rights.**
- **One user may back at most one member per group.** A request that would link a user already linked
  to another member in the same group is refused (enforced in the application layer — the domain holds
  only an identifier).
- A [managed member](../../glossary/managed-member.md) **cannot** be linked directly; they must first
  be **promoted** in the circle ([FR-019](FR-019-manage-managed-members.md)), which supplies the user
  reference.
- **Unlinking** reverts the member to unlinked behaviour: wishlist ownership returns to the organizer,
  the user loses in-app read access to the assignment, and **the member and their wishlist content are
  not deleted**.
- **Account deletion cascades to a graceful unlink.** When a linked user deletes their account
  ([FR-015](FR-015-delete-account.md)), every member referencing it is unlinked; the member, its name
  and its wishlist survive, and the group does not break.

## Rationale

Linking is what turns "a name in a group" into "a person who sees their own assignment and owns their
own wishlist" ([ADR-0019](../../adr/0019-member-user-linking.md)). It must stay **additive**: unlinked
members remain first-class and mixed groups are the normal case, so the account-free product is never
weakened ([NFR-001](../non-functional/NFR-001-local-first-offline.md)).

Consent is not optional politeness. A link both **asserts an identity** ("this member is that person")
and **grants a privacy capability** (read access to an assignment once drawn), so it cannot be imposed
unilaterally. Routing it through a request the recipient resolves also keeps the
enumeration-resistance promise: the organizer learns nothing about whether an address has an account
([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md), decision D5).

The duplicate-link guard exists because a person cannot give a gift to themselves twice; the deletion
cascade exists because a dangling account reference must degrade to unlinked rather than break a group
that other people depend on.

## Acceptance criteria

```gherkin
Given an organizer viewing an unlinked member
When they send a link request to an email address
Then the response is "request sent"
And it does not reveal whether that address has an account
And the member is shown as link pending

Given a link request to a real account
When the recipient accepts it
Then the member becomes linked to that user
And wishlist ownership moves to that user

Given a link request
When the recipient declines it or it expires
Then the member stays unlinked
And the organizer is not told which of the two occurred

Given a signed-in organizer linking their own account to a member
When they confirm
Then the link is established without a separate acceptance step

Given a user already linked to a member in a group
When a request would link them to a second member in the same group
Then it is refused

Given a managed member
When an organizer attempts to link them to an account directly
Then it is refused
And the promotion path is offered instead

Given a linked member
When the organizer unlinks them
Then the member reverts to unlinked
And its wishlist content is retained under the organizer
And the previously linked user can no longer read that assignment

Given a member linked to a user
When that user deletes their account
Then the member is unlinked
And the member, its name and its wishlist survive
And the group opens without error

Given a mixed group of linked and unlinked members
When reveal links are generated
Then the unlinked members receive reveal links exactly as before
```

## Dependencies & related

- Derived from: [UC-SS-010](../../use-cases/secret-santa/UC-SS-010-link-member-to-account.md)
- Related: [FR-027](FR-027-create-group-from-circle.md) (establishes the same link implicitly),
  [FR-029](FR-029-see-my-assignment.md) and [FR-030](FR-030-edit-my-wishlist.md) (what the link
  unlocks), [FR-018](FR-018-respond-to-circle-invitation.md) (the invitation-acceptance pattern this
  mirrors), [FR-015](FR-015-delete-account.md) (the deletion cascade),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADRs: [ADR-0019](../../adr/0019-member-user-linking.md) (optional `UserId`, unlink,
  duplicate-link guard, deletion cascade), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
  (what the link grants), [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (managed-member
  promotion)
- Implemented by: [FEAT-005](../../features/FEAT-005-m4-linking.md) *(M4 — linking; designed, `Draft`)*, verified by [TP-005](../../features/TP-005-m4-linking.md)
- ~~Open for the feature spec: whether the link request **reuses the M2
  [Invitation](../../glossary/invitation.md) infrastructure** (rate limits, expiry, revoke) or gets a
  member-link variant of it.~~ **Settled in [FEAT-005](../../features/FEAT-005-m4-linking.md):** it
  **reuses** the invitation machinery (outbox mail, expiry, revoke, constant-time account lookup) as a
  member-link variant scoped to a member rather than a circle join — not a parallel system.
- Glossary: [Member](../../glossary/member.md), [User](../../glossary/user.md),
  [Wishlist](../../glossary/wishlist.md), [Assignment](../../glossary/assignment.md)
