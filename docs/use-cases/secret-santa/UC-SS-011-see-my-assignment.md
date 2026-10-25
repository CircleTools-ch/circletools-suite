---
id: UC-SS-011
title: See my assignment in the app
app: Secret-Santa
actors: [Participant, Organizer in the roster]
status: Approved
priority: Must
related: [FR-029, FR-032, NFR-002, ADR-0086, ADR-0014]
---

# UC-SS-011 — See my assignment in the app

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

A person taking part sees whom they give to, per event, on their own device — without their reveal link.

## Primary actor & stakeholders

- **Primary actor:** a participant (or an organizer who is also in the roster).
- **Stakeholders & interests:** everyone else in the group — that nobody sees more than their own pairing.

## Preconditions

- The person takes part in an online group (through an account or this device).

## Postconditions

- **Success:** the person has seen their own pairing for the events drawn so far.
- **Failure:** the page shows the last view this device received, or says the group could not be loaded.

## Trigger

The person opens the group from *Groups you take part in* (an organizer: *My assignment* on the group's page).

## Main success scenario

1. The system shows the participant page, refreshing it from the server in the background.
2. For each drawn event, the system shows the reveal step; for an event not drawn yet, it says so.
3. The person chooses *Reveal who you got*.
4. The system shows the recipient's name, wishlist and gift note, the budget and the place, with *Add to calendar*.

## Extensions / alternate flows

- **1a.** Offline → the last view the device received is shown.
- **1b.** The person was freed or the group deleted → the group disappears from the device.

## Exceptions

- **None.**

## Non-functional notes

- **Offline/local-first:** the last view stays readable offline.
- **Localization:** dates and budgets in the person's culture.
- **Privacy:** the view holds only the person's own pairing per event (NFR-002).
- **Performance:** shows what the device holds at once, then refreshes.

## Open questions

- None.

## Traceability

- Requirements: [FR-029](../../requirements/functional/FR-029-see-my-assignment.md), [FR-032](../../requirements/functional/FR-032-add-organizer-as-member.md), [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Participant](../../glossary/participant.md), [Assignment](../../glossary/assignment.md)
