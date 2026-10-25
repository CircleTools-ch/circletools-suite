---
id: UC-SS-010
title: Invite a person by email to take part
app: Secret-Santa
actors: [Organizer, Invited person]
status: Approved
priority: Must
related: [FR-028, FR-041, FR-042, NFR-002, NFR-005, ADR-0086]
---

# UC-SS-010 — Invite a person by email to take part

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

The organizer invites someone by email to take part as a member of an online group; the person accepts in their own app and then sees their own pairing and keeps their own wishlist.

## Primary actor & stakeholders

- **Primary actor:** the organizer.
- **Stakeholders & interests:** the invited person — to take part on their own devices; everyone — that an address reveals nothing about whether it has an account.

## Preconditions

- The group is online and the organizer may manage participants.
- The member has no participant.

## Postconditions

- **Success:** the member shows *Invited*; once accepted, the person is a participant of that member.
- **Failure:** nothing changes; the member stays free.

## Trigger

The organizer chooses *Invite by email…* on a member's row.

## Main success scenario

1. The organizer enters an email address and sends the invitation.
2. The system records the invitation for 30 days and shows the member as *Invited*.
3. If the address belongs to an account, the invitation appears in that account's group list under *Invitations*, and an email points there.
4. The invited person chooses *Take part*.
5. The system makes the account a participant of the member; the group appears among the groups it takes part in, on all its devices.
6. The person opens the group and sees their participant page.

## Extensions / alternate flows

- **3a.** The address has no account → the organizer sees the same answer; nothing appears anywhere until someone registers with it within 30 days.
- **4a.** The person declines → the invitation goes; the member is free again.
- **4b.** Nobody answers within 30 days → the invitation lapses.
- **1a.** The account already takes part in the group → the organizer is told.

## Exceptions

- **Offline** → the invitation can't be sent; the organizer is told to try again online.

## Non-functional notes

- **Offline/local-first:** needs a connection; groups on this device have no invitations.
- **Localization:** the email and the app texts in the invitee's language.
- **Privacy:** the answer never says whether an address has an account (NFR-005).
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-028](../../requirements/functional/FR-028-link-member-to-account.md), [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md), [FR-042](../../requirements/functional/FR-042-release-a-claimed-person.md), [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Participant](../../glossary/participant.md), [Role assignment](../../glossary/role-assignment.md), [Member](../../glossary/member.md)
