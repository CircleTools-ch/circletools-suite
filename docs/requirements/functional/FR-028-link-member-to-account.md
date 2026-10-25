---
id: FR-028
title: Invite a person by email to take part as a member
type: functional
status: Implemented
priority: Must
source: UC-SS-010
verification: test
related: [UC-SS-010, UC-SS-011, UC-SS-012, FR-041, FR-042, NFR-002, NFR-005, ADR-0086, FEAT-027]
---

# FR-028 — Invite a person by email to take part as a member

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

From a member's row in an online group, the organizer can **invite an email address** to take part as that member:

- The invitation shows on the member as *Invited* and expires after 30 days if nobody answers.
- If the address belongs to an account, the invitation appears in that account's group list under
  *Invitations*, with *Take part* and *Decline*, and an email points there.
- Accepting makes the account a [participant](../../glossary/participant.md) of the member; the group then appears among the
  groups the account takes part in, on all its devices. Declining removes the invitation.
- The answer to the organizer is the same whether or not the address has an account
  ([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)), except when that account already takes part in
  the group, which is reported.
- The organizer can withdraw the invitation (free the spot, [FR-042](FR-042-release-a-claimed-person.md)).

Inviting is one of four routes to a participant ([FR-041](FR-041-join-link-and-person-claim.md)).

## Rationale

The organizer often knows people's addresses but not whether they have an account; an invitation lets a person
take part without the organizer sending them a link.

## Acceptance criteria

```gherkin
Given an online group and a member without a participant
When the organizer invites an address that has an account
Then the member shows "Invited" and the account sees the invitation in its group list

Given the invited account takes part
When it opens the group
Then it sees its own participant page for that member

Given an address without an account
When the organizer invites it
Then the organizer sees the same answer as for an address with an account

Given an invitation nobody answered for 30 days
When the invitee looks for it
Then it is gone
```

## Dependencies & related

- Derived from: [UC-SS-010](../../use-cases/secret-santa/UC-SS-010-link-member-to-account.md)
- Related: [FR-041](FR-041-join-link-and-person-claim.md), [FR-042](FR-042-release-a-claimed-person.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
