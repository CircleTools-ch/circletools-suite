---
id: FR-018
title: Accept or decline a circle invitation
type: functional
status: Approved
priority: Must
source: UC-MAIN-010
verification: test
related: [UC-MAIN-010, UC-MAIN-009, UC-MAIN-001, NFR-005, NFR-006, ADR-0018]
---

# FR-018 — Accept or decline a circle invitation

## Description

An invited person must be able to **accept** or **decline** an
[Invitation](../../glossary/invitation.md), by one of two paths decided entirely on their side:

| The invited address | The recipient sees |
|---|---|
| has **no** account | a mail link leading to sign-up with the **invited address pre-filled and locked**; the membership is created once the address is verified |
| already has an account | a mail telling them to open the app, and an in-app **join request** they accept or decline while signed in |

**The invariant both paths share:** a membership is only ever granted to an account whose **verified**
email address is the invited one. The mailed link is not a bearer key — forwarding it lets someone
read the invitation, never join the circle.

Accepting creates a `member` [membership](../../glossary/circle-membership.md) and consumes the
invitation. Declining consumes it and leaves no membership. **Declining must be possible on both
paths**, without an account. Accepting when already a member of that circle is reported to the
recipient and creates no second membership.

## Rationale

The forwarded-mail hole is the obvious failure mode of an emailed invitation: a family mail thread is
exactly where such a link gets passed around. Binding the membership to a *verified* address closes it
without a second secret and without asking the recipient to prove anything twice — the sign-up path
already verifies, and the account path already knows who is signed in.

Decline-without-an-account looks like a convenience and is actually the enumeration control: if only
account holders could decline, then "declined" appearing in the inviter's list would prove that the
address has an account — reopening the oracle
[ADR-0018](../../adr/0018-circle-and-sharing-model.md) closed at real UX cost. With decline available
on both paths, the inviter learns nothing from it.

## Acceptance criteria

```gherkin
Given a pending invitation for an address with no account
When the recipient opens the link and completes sign-up and verification
Then a member membership is created in that circle
And the invitation is consumed

Given a pending invitation for an address with no account
When the recipient opens the link
Then the sign-up address field is pre-filled with the invited address
And it cannot be changed

Given a pending invitation for an address that already has an account
When the account holder signs in
Then the join request is shown in the app
And accepting it creates a member membership without any token from the mail

Given a pending invitation for one address
When a user signed in with a different address opens the link
Then no membership is created for them
And they are not told whether the invited address has an account

Given a pending invitation
When the recipient declines
Then no membership is created
And the invitation is consumed

Given a pending invitation for an address with no account
When the recipient opens the link
Then a decline action is available without creating an account

Given an invitation accepted by someone already in the circle
When they accept
Then they are told they are already in the circle
And no second membership exists

Given an invitation that has expired, been revoked, or already been used
When the recipient attempts to accept it
Then they are told plainly that it is no longer valid
And they are offered to ask the inviter for a new one

Given a circle deleted after an invitation was sent
When the recipient opens the invitation
Then they are told the circle no longer exists

Given a recipient who abandons sign-up partway
When they return to the same link before it expires
Then the invitation is still pending and can be completed

Given an invitation accepted
When the new member views the circle
Then they see its people
And before accepting they saw only the circle name, its label and the inviter's display name
```

## Dependencies & related

- Derived from: [UC-MAIN-010](../../use-cases/main/UC-MAIN-010-respond-to-invitation.md)
- Related: [FR-017](FR-017-invite-to-circle.md), [FR-007](FR-007-create-suite-account.md)
  (the sign-up path it reuses), [FR-020](FR-020-manage-circle-roles-and-membership.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D5 — the branch
  resolves on the recipient's side)
- **Open for the M2 feature spec:** whether the inviter is notified of an acceptance beyond the person
  appearing in the list; the re-invite cooldown after a decline.
- Glossary: [Invitation](../../glossary/invitation.md),
  [Circle membership](../../glossary/circle-membership.md), [User](../../glossary/user.md)
