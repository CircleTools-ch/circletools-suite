---
id: FR-017
title: Invite a person to a circle by email
type: functional
status: Approved
priority: Must
source: UC-MAIN-009
verification: test
related: [UC-MAIN-009, UC-MAIN-010, NFR-003, NFR-005, NFR-006, ADR-0018]
---

# FR-017 — Invite a person to a circle by email

## Description

A circle **owner** or **admin** must be able to invite a person to a
[Circle](../../glossary/circle.md) by email address. The system creates a pending
[Invitation](../../glossary/invitation.md), queues a mail, and answers the inviter with
**"invitation sent"** — the identical response in every case, including when the address already has
an account and when it already belongs to someone in the circle.

An invitation:

- has an **expiry** and is **single-use**;
- appears in the circle's **pending invitations** list, where an owner or admin can **resend** or
  **revoke** it;
- always leads to a `member` membership — no role above `member` may be granted by invitation;
- is subject to **rate limits** per inviter, per address and per circle.

Inviting the same address twice **resends** the existing invitation rather than creating a second one.

## Rationale

This form is the most tempting account-enumeration oracle in the product: "does this address already
have an account?" is precisely the question an inviter would like answered and precisely the probe an
attacker uses. [ADR-0018](../../adr/0018-circle-and-sharing-model.md) accepted the UX cost of never
answering it, and [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md) generalises
that to every address-accepting surface. Rate limiting belongs to the same property, not beside it: an
identical response is defeated by volume if nothing bounds it, and resend without a limit is a
mail-bombing tool pointed at a third party.

Invitations grant only `member` because the accept step should not be a privilege negotiation —
promotion is a deliberate, separate act by the owner
([FR-020](FR-020-manage-circle-roles-and-membership.md)).

## Acceptance criteria

```gherkin
Given a circle admin inviting an address that has no account
And a circle admin inviting an address that already has an account
When each invitation is sent
Then the two responses are identical in body, status code and timing distribution
And both read "invitation sent"

Given a circle admin inviting an address that already belongs to a member of that circle
When the invitation is sent
Then the response is identical to the other two cases
And no duplicate membership can result from it

Given a pending invitation for an address
When the same address is invited again in the same circle
Then the existing invitation is resent rather than duplicated

Given a pending invitation
When an owner or admin revokes it
Then it can no longer be accepted
And an accept attempt is told plainly that it is no longer valid

Given a pending invitation
When it passes its expiry unaccepted
Then it leaves the pending list
And the address may be invited again

Given a user holding only the member role in a circle
When they call the invite endpoint directly
Then it is rejected server-side, not merely hidden in the UI

Given an inviter who exceeds the invitation rate limit
When they send another invitation
Then it is refused with a message saying when they may continue

Given an invitation whose mail cannot be delivered
When delivery fails
Then the inviter is not told
And the invitation remains pending and resendable

Given an invitation is sent
When the recipient address has an account with a preferred language
Then the mail is in that language
And otherwise it is in the inviter's language

Given any invitation is sent
When the response is returned
Then mail dispatch has been queued, not awaited
```

## Dependencies & related

- Derived from: [UC-MAIN-009](../../use-cases/main/UC-MAIN-009-invite-to-circle.md)
- Related: [FR-018](FR-018-respond-to-circle-invitation.md),
  [FR-016](FR-016-create-circle.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D5)
- **Open for the M2 feature spec:** invitation lifetime; the rate-limit numbers per inviter, per
  address and per circle; the re-invite cooldown after a decline.
- **Blocked on:** the transactional-email provider decision — the same dependency M1 carries for
  verification and reset mail ([backlog](../../roadmap/backlog.md)).
- Glossary: [Invitation](../../glossary/invitation.md), [Circle](../../glossary/circle.md)
