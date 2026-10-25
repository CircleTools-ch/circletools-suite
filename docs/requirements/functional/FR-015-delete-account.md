---
id: FR-015
title: Delete the account
type: functional
status: Implemented
priority: Must
source: UC-MAIN-006
verification: test
related: [UC-MAIN-006, NFR-001, NFR-004, ADR-0017, ADR-0018, ADR-0019, ADR-0022]
---

# FR-015 — Delete the account

## Description

A signed-in [User](../../glossary/user.md) must be able to delete their account. Deletion is
**immediate and irreversible server-side** — there is no grace period and no soft-delete state.

On confirmation and **recent re-authentication**, the system:

- deletes the account, its credentials, its [Passkeys](../../glossary/passkey.md), and every session
  and refresh token;
- removes every role the account held in Secret-Santa [online groups](../../glossary/online-group.md) —
  as a [participant](../../glossary/participant.md) or an invitation — **leaving the member and what it wrote in
  place**, and hands over or deletes the groups it owns as the user chose
  ([FR-043](FR-043-release-ownership-and-deletion-disposition.md), [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md));
- **does not rewrite completed [Draws](../../glossary/draw.md)** — history stands and already-sent
  [reveal links](../../glossary/reveal-link.md) keep working;
- **does not delete local data on the user's devices**, which stays usable account-free.

Before deletion proceeds, the user must be shown **precisely what will and will not be deleted**. A
user who is the **sole owner of a [Circle](../../glossary/circle.md) must transfer ownership or delete
the circle first** — deletion may not silently orphan a circle.

## Rationale

Deletion is a legal obligation once personal data is stored server-side. Making it immediate rather
than deferred is defensible **specifically because**
[ADR-0017](../../adr/0017-anonymous-to-account-claim.md) keeps the local device copy: the blast radius
of a mistaken deletion is lost sync and lost circle memberships, not lost data. A grace period would
buy protection against a loss the local-first architecture already prevents, at the cost of a
lifecycle state every query, uniqueness check and invitation lookup would then have to reason about.

Leaving members and draws intact reflects whose data they are: a member in someone else's group is
that organizer's record, and a completed draw is history that other people's reveal links still depend
on.

## Acceptance criteria

```gherkin
Given a signed-in user who has re-authenticated recently
When they confirm account deletion
Then the account, its credentials and passkeys are deleted
And every session and refresh token is revoked immediately

Given a user about to confirm deletion
When the confirmation screen is shown
Then it states precisely what will be deleted and what will not
And it states that local data on their devices is unaffected

Given a deleted account that was linked to Secret-Santa members
When deletion completes
Then each member's UserId link is removed
And each member record itself remains in its organizer's group

Given a completed draw involving the deleted user
When deletion completes
Then the draw is unchanged
And previously issued reveal links still resolve

Given a user who is the sole owner of a circle
When they attempt to delete their account
Then deletion is blocked until they transfer ownership or delete the circle

Given a user who deletes their account
When they reopen the app on their own device
Then their local data is still present and usable account-free

Given a deleted account
When someone signs up again with the same email address
Then an entirely new account is created with no data from the old one

Given a deletion that fails partway
When the failure occurs
Then the account is not left partially deleted
And the user is not told their data is gone
```

## Dependencies & related

- Derived from: [UC-MAIN-006](../../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Related: [FR-014](FR-014-export-personal-data.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-004](../non-functional/NFR-004-data-protection-and-retention.md)
- Governing ADRs: [ADR-0022](../../adr/0022-account-deletion-and-data-export.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md),
  [ADR-0019](../../adr/0019-member-user-linking.md)
- **Closed in M2 (2026-07-30):** [Managed members](../../glossary/managed-member.md) created by a
  departing user are circle data and survive with the circle, as ADR-0022's principle implied and
  [FR-019](FR-019-manage-managed-members.md) states outright. The **sole-circle-owner guard is built**
  as `SoleOwnerCircleGuard` behind M1's `IAccountDeletionGuard`, and this requirement is now
  `Implemented`. Two details are worth carrying forward:
  - it blocks on **every** circle the account owns, not only those nobody else could own — deleting
    the owner's own membership would otherwise leave a circle with no owner, a state the domain never
    defines because `Circle.Leave` guarantees it cannot occur. Each blocker names itself and carries
    the way out that fits it: transfer ([FR-020](FR-020-manage-circle-roles-and-membership.md)) when
    somebody else could take it, deletion ([FR-022](FR-022-delete-circle.md)) when nobody could;
  - deletion needed a **second seam**. `IAccountDeletionGuard` answers *may this be deleted*; the new
    `IAccountDeletionCleanup` removes the account's remaining memberships and the invitations it sent,
    or the people list would keep a person who is nobody. The two run in a fixed **order** rather than
    one transaction — guards, sessions, cleanups, then the account row — so a partial failure leaves
    the recoverable state ([arc42 ch. 8](../../arc42/08-crosscutting-concepts.md)).
- Glossary: [User](../../glossary/user.md), [Member](../../glossary/member.md),
  [Draw](../../glossary/draw.md), [Circle](../../glossary/circle.md),
  [Passkey](../../glossary/passkey.md)
