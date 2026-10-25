---
id: UC-SS-009
title: Create a group from a circle
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Must
related: [FR-027, FR-019, FR-022, FR-041, NFR-001, NFR-006, ADR-0086, ADR-0018]
---

# UC-SS-009 — Create a group from a circle

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

The organizer starts a group from one of their circles, so the people who belong together are in it and those with an account take part straight away.

## Primary actor & stakeholders

- **Primary actor:** a signed-in organizer.
- **Stakeholders & interests:** the circle's people — to take part without being invited again.

## Preconditions

- The organizer is signed in and belongs to a circle with at least two people.

## Postconditions

- **Success:** an online group exists, owned by the organizer; every chosen person is a member; those with an account take part.
- **Failure:** no group is created, or (offline) the group is kept on this device.

## Trigger

The organizer chooses *Create from a circle* in the group list.

## Main success scenario

1. The system shows the organizer's circles and their people.
2. The organizer picks a circle, names the group and chooses the people.
3. The system creates the group with one member per chosen person, recording each origin.
4. The system puts the group online, owned by the organizer's account.
5. The system makes every chosen person with an account a participant of their member, after the server checks the circle is shared with the organizer; the organizer's own member is connected to their account.
6. The system opens the new group.

## Extensions / alternate flows

- **3a.** Two chosen people resolve to the same account → the first takes part; the others are members without it, and the organizer is told which.
- **4a.** The device is offline → the group stays on this device; it can be put online later (UC-SS-017).

## Exceptions

- **The circle is no longer shared with the organizer** → those people are members without taking part.

## Non-functional notes

- **Offline/local-first:** the group is still created, on this device.
- **Localization:** names are user data and shown as written.
- **Privacy:** the group's document carries no account ids; who takes part lives in the group store.
- **Performance:** one request per person taking part; nothing blocks creating the group.

## Open questions

- None.

## Traceability

- Requirements: [FR-027](../../requirements/functional/FR-027-create-group-from-circle.md), [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md), [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Circle](../../glossary/circle.md), [Member](../../glossary/member.md), [Participant](../../glossary/participant.md), [Online group](../../glossary/online-group.md)
