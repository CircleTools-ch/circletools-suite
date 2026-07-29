---
id: FR-015
title: Delete the account
type: functional
status: Approved
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
- removes the `UserId` link from any Secret-Santa [Members](../../glossary/member.md) that pointed at
  the account, **leaving the member itself in place**
  ([ADR-0019](../../adr/0019-member-user-linking.md));
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
- **Answered in M2 (2026-07-29):** what happens to [Managed members](../../glossary/managed-member.md)
  created by a departing user — they are circle data and survive with the circle, as ADR-0022's
  principle implied and [FR-019](FR-019-manage-managed-members.md) now states outright. The
  sole-circle-owner guard is specified by [FR-022](FR-022-delete-circle.md) (delete the circle) and
  [FR-020](FR-020-manage-circle-roles-and-membership.md) (transfer ownership); it remains unbuilt
  until M2 ships.
- Glossary: [User](../../glossary/user.md), [Member](../../glossary/member.md),
  [Draw](../../glossary/draw.md), [Circle](../../glossary/circle.md),
  [Passkey](../../glossary/passkey.md)
