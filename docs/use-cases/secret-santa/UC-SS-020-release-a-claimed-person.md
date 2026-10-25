---
id: UC-SS-020
title: Free a participant's spot so the member can be taken again
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Could
related: [FR-042, FR-041, UC-SS-019, ADR-0086]
---

# UC-SS-020 — Free a participant's spot so the member can be taken again

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

The organizer frees a member whose participant lost their device or picked the wrong name, so the right person can take part.

## Primary actor & stakeholders

- **Primary actor:** the organizer.
- **Stakeholders & interests:** the freed person — that what they wrote stays.

## Preconditions

- The member has a participant or a pending invitation.

## Postconditions

- **Success:** the member is free; what the person wrote stays in the group.
- **Failure:** nothing changes.

## Trigger

The organizer chooses *Free this spot* on a member's row.

## Main success scenario

1. The system asks to confirm, saying access ends and what was written stays.
2. The organizer confirms.
3. The system removes the participant (or the invitation); the member no longer shows *Connected* or *Invited*.
4. The person joins again through the join link, or is invited again.

## Extensions / alternate flows

- **1a.** The participant leaves the group themselves → same result.

## Exceptions

- **Offline** → the change is refused until the device is online.

## Non-functional notes

- **Offline/local-first:** needs a connection.
- **Localization:** —
- **Privacy:** the freed device can no longer read the group.
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-042](../../requirements/functional/FR-042-release-a-claimed-person.md), [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Participant](../../glossary/participant.md), [Member entry](../../glossary/member-entry.md)
